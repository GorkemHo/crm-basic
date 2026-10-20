using CRM.BuildingBlocks.Application;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Features.Deals;

/// <summary>Sum of deals in one currency (amounts in different currencies are never added together).</summary>
internal sealed record CurrencyTotal(string Currency, int Count, decimal Amount, decimal WeightedAmount);

internal sealed record BoardColumn(int StageId, string Name, int Probability, IReadOnlyList<DealDto> Deals, IReadOnlyList<CurrencyTotal> Totals);

// ---------------------------------------------------------------- Board

/// <summary>Open deals grouped by stage, in board order.</summary>
internal sealed class GetBoard : IQuery<IReadOnlyList<BoardColumn>>
{
    public int? CompanyId { get; set; }
}

internal sealed class GetBoardHandler : IRequestHandler<GetBoard, IReadOnlyList<BoardColumn>>
{
    private readonly SalesDbContext _db;
    private readonly DealNames _names;

    public GetBoardHandler(SalesDbContext db, DealNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<IReadOnlyList<BoardColumn>> Handle(GetBoard request, CancellationToken cancellationToken)
    {
        var stages = await _db.Stages.AsNoTracking().OrderBy(x => x.Position).ToListAsync(cancellationToken);

        var query = _db.Deals.AsNoTracking().Where(x => x.Status == DealStatus.Open);
        if(request.CompanyId.HasValue) query = query.Where(x => x.CompanyId == request.CompanyId);

        var deals = await _names.FillAsync(
            await query.OrderBy(x => x.ExpectedCloseDate == null).ThenBy(x => x.ExpectedCloseDate).ThenBy(x => x.Id)
                .Select(DealDto.Projection).ToListAsync(cancellationToken),
            cancellationToken);

        return stages.Select(stage =>
        {
            var inStage = deals.Where(d => d.StageId == stage.Id).ToList();
            var totals = inStage
                .GroupBy(d => d.Currency)
                .OrderBy(g => g.Key)
                .Select(g => new CurrencyTotal(g.Key, g.Count(), g.Sum(d => d.Amount), PipelineMath.Weighted(g.Sum(d => d.Amount), stage.Probability)))
                .ToList();

            return new BoardColumn(stage.Id, stage.Name, stage.Probability, inStage, totals);
        }).ToList();
    }
}

// ---------------------------------------------------------------- Summary (dashboard)

internal sealed record PipelineSummary(
    IReadOnlyList<CurrencyTotal> Open,
    IReadOnlyList<CurrencyTotal> WonThisMonth,
    int LostThisMonth);

internal sealed record GetPipelineSummary : IQuery<PipelineSummary>;

internal sealed class GetPipelineSummaryHandler : IRequestHandler<GetPipelineSummary, PipelineSummary>
{
    private readonly SalesDbContext _db;
    private readonly TimeProvider _clock;

    public GetPipelineSummaryHandler(SalesDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PipelineSummary> Handle(GetPipelineSummary request, CancellationToken cancellationToken)
    {
        var open = await _db.Deals.AsNoTracking()
            .Where(x => x.Status == DealStatus.Open)
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Count(), g.Sum(d => d.Amount), g.Sum(d => d.Amount * d.Stage.Probability / 100m)))
            .ToListAsync(cancellationToken);

        var now = _clock.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var won = await _db.Deals.AsNoTracking()
            .Where(x => x.Status == DealStatus.Won && x.ClosedAt >= monthStart)
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Count(), g.Sum(d => d.Amount), g.Sum(d => d.Amount)))
            .ToListAsync(cancellationToken);

        var lost = await _db.Deals.CountAsync(x => x.Status == DealStatus.Lost && x.ClosedAt >= monthStart, cancellationToken);

        return new PipelineSummary(
            open.Select(PipelineMath.Round).OrderBy(x => x.Currency).ToList(),
            won.OrderBy(x => x.Currency).ToList(),
            lost);
    }
}

internal static class PipelineMath
{
    /// <summary>Expected value of <paramref name="amount"/> at <paramref name="probability"/>%, rounded to cents.</summary>
    public static decimal Weighted(decimal amount, int probability) =>
        Math.Round(amount * probability / 100m, 2, MidpointRounding.AwayFromZero);

    public static CurrencyTotal Round(CurrencyTotal total) =>
        total with { WeightedAmount = Math.Round(total.WeightedAmount, 2, MidpointRounding.AwayFromZero) };
}
