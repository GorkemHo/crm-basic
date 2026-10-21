using CRM.BuildingBlocks.Reporting;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Features.Deals;
using CRM.Modules.Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Infrastructure;

internal sealed class SalesReports : ISalesReports
{
    private readonly SalesDbContext _db;

    public SalesReports(SalesDbContext db) => _db = db;

    public async Task<SalesReport> GetSalesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var closed = _db.Deals.AsNoTracking().Where(x => x.ClosedAt >= fromUtc && x.ClosedAt < toUtc);
        var won = closed.Where(x => x.Status == DealStatus.Won);
        var lost = closed.Where(x => x.Status == DealStatus.Lost);

        var wonByMonth = await won
            .GroupBy(x => new { x.ClosedAt!.Value.Year, x.ClosedAt!.Value.Month, x.Currency })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Currency, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var lostByMonth = await lost
            .GroupBy(x => new { x.ClosedAt!.Value.Year, x.ClosedAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var months = ReportMonths.Between(fromUtc, toUtc)
            .Select(m => new MonthlySales(
                m.Year,
                m.Month,
                wonByMonth.Where(x => x.Year == m.Year && x.Month == m.Month)
                    .OrderBy(x => x.Currency)
                    .Select(x => new CurrencyAmount(x.Currency, x.Count, x.Amount))
                    .ToList(),
                lostByMonth.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.Count)))
            .ToList();

        var wonTotals = wonByMonth
            .GroupBy(x => x.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyAmount(g.Key, g.Sum(x => x.Count), g.Sum(x => x.Amount)))
            .ToList();

        var wonCount = wonTotals.Sum(x => x.Count);
        var lostCount = lostByMonth.Sum(x => x.Count);

        // Days between creating and winning; averaged in memory (only won deals of the period).
        var cycles = await won.Select(x => new { x.CreatedAt, ClosedAt = x.ClosedAt!.Value }).ToListAsync(cancellationToken);

        var reasons = await lost
            .GroupBy(x => x.LostReason)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Take(10)
            .Select(g => new LostReasonCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return new SalesReport(
            months,
            wonTotals,
            wonCount,
            lostCount,
            wonCount + lostCount == 0 ? null : (double)wonCount / (wonCount + lostCount),
            cycles.Count == 0 ? null : Math.Round(cycles.Average(c => (c.ClosedAt - c.CreatedAt).TotalDays), 1),
            reasons);
    }

    public async Task<IReadOnlyList<PipelineStageReport>> GetPipelineAsync(CancellationToken cancellationToken)
    {
        var stages = await _db.Stages.AsNoTracking().OrderBy(x => x.Position).ToListAsync(cancellationToken);

        var totals = await _db.Deals.AsNoTracking()
            .Where(x => x.Status == DealStatus.Open)
            .GroupBy(x => new { x.StageId, x.Currency })
            .Select(g => new { g.Key.StageId, g.Key.Currency, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        return stages.Select(s => new PipelineStageReport(
                s.Id,
                s.Name,
                s.Probability,
                totals.Where(t => t.StageId == s.Id)
                    .OrderBy(t => t.Currency)
                    .Select(t => new StageCurrencyTotal(t.Currency, t.Count, t.Amount, PipelineMath.Weighted(t.Amount, s.Probability)))
                    .ToList()))
            .ToList();
    }
}
