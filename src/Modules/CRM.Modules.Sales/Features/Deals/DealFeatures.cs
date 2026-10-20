using System.Linq.Expressions;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Features.Deals;

/// <param name="CompanyName">From the Crm module; null if the company can no longer be found.</param>
internal sealed record DealDto(
    int Id,
    string Title,
    int CompanyId,
    string? CompanyName,
    int? CustomerId,
    string? CustomerName,
    decimal Amount,
    string Currency,
    int StageId,
    string StageName,
    int Probability,
    string Status,
    DateOnly? ExpectedCloseDate,
    DateTime? ClosedAt,
    string? LostReason,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Names from other modules are filled in by <see cref="DealNames"/>.</summary>
    public static readonly Expression<Func<Deal, DealDto>> Projection = d => new DealDto(
        d.Id, d.Title, d.CompanyId, null, d.CustomerId, null, d.Amount, d.Currency,
        d.StageId, d.Stage.Name, d.Stage.Probability, d.Status, d.ExpectedCloseDate, d.ClosedAt, d.LostReason,
        d.CreatedAt, d.UpdatedAt);
}

/// <summary>Adds company and contact names (owned by the Crm module) to deals.</summary>
internal sealed class DealNames
{
    private readonly ICrmDirectory _directory;

    public DealNames(ICrmDirectory directory) => _directory = directory;

    public async Task<IReadOnlyList<DealDto>> FillAsync(IReadOnlyList<DealDto> deals, CancellationToken cancellationToken)
    {
        if(deals.Count == 0)
        {
            return deals;
        }

        var companies = await _directory.GetCompaniesAsync(deals.Select(d => d.CompanyId), cancellationToken);
        var customers = await _directory.GetCustomersAsync(deals.Where(d => d.CustomerId.HasValue).Select(d => d.CustomerId!.Value), cancellationToken);

        return deals.Select(d => d with
        {
            CompanyName = companies.GetValueOrDefault(d.CompanyId)?.CompanyName,
            CustomerName = d.CustomerId is { } customerId ? customers.GetValueOrDefault(customerId)?.FullName : null
        }).ToList();
    }

    public async Task<DealDto> FillAsync(DealDto deal, CancellationToken cancellationToken) =>
        (await FillAsync([deal], cancellationToken))[0];
}

// ---------------------------------------------------------------- List / get

internal sealed class GetDeals : PaginationRequest, IQuery<PagedResponse<DealDto>>
{
    /// <summary>Matches the title (case-insensitive).</summary>
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int? StageId { get; set; }

    public int? CompanyId { get; set; }

    public int? CustomerId { get; set; }

    public string? Currency { get; set; }
}

internal sealed class GetDealsHandler : IRequestHandler<GetDeals, PagedResponse<DealDto>>
{
    private readonly SalesDbContext _db;
    private readonly DealNames _names;

    public GetDealsHandler(SalesDbContext db, DealNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<PagedResponse<DealDto>> Handle(GetDeals request, CancellationToken cancellationToken)
    {
        var query = _db.Deals.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => EF.Functions.ILike(x.Title, $"%{request.Search.Trim()}%"));
        }

        if(!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status);
        if(request.StageId.HasValue) query = query.Where(x => x.StageId == request.StageId);
        if(request.CompanyId.HasValue) query = query.Where(x => x.CompanyId == request.CompanyId);
        if(request.CustomerId.HasValue) query = query.Where(x => x.CustomerId == request.CustomerId);
        if(!string.IsNullOrWhiteSpace(request.Currency)) query = query.Where(x => x.Currency == request.Currency);

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderByDescending(x => x.UpdatedAt);
        }

        var page = await query.ToPagedResponseAsync(request, DealDto.Projection, cancellationToken);

        return new PagedResponse<DealDto>
        {
            Items = await _names.FillAsync(page.Items, cancellationToken),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }
}

internal sealed record GetDeal(int Id) : IQuery<DealDto>;

internal sealed class GetDealHandler : IRequestHandler<GetDeal, DealDto>
{
    private readonly SalesDbContext _db;
    private readonly DealNames _names;

    public GetDealHandler(SalesDbContext db, DealNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<DealDto> Handle(GetDeal request, CancellationToken cancellationToken)
    {
        var deal = await _db.Deals.AsNoTracking().Where(x => x.Id == request.Id).Select(DealDto.Projection).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Deal", request.Id);

        return await _names.FillAsync(deal, cancellationToken);
    }
}

// ---------------------------------------------------------------- Create / update / delete

/// <param name="StageId">Defaults to the first stage of the pipeline.</param>
internal sealed record CreateDeal(
    string Title,
    int CompanyId,
    int? CustomerId,
    decimal Amount,
    string Currency,
    int? StageId,
    DateOnly? ExpectedCloseDate) : ICommand<DealDto>;

internal sealed class CreateDealValidator : AbstractValidator<CreateDeal>
{
    public CreateDealValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Currency).Must(Currencies.All.Contains)
            .WithMessage($"Supported currencies: {string.Join(", ", Currencies.All)}.");
    }
}

internal sealed class CreateDealHandler : IRequestHandler<CreateDeal, DealDto>
{
    private readonly SalesDbContext _db;
    private readonly ICrmDirectory _directory;
    private readonly ISender _sender;

    public CreateDealHandler(SalesDbContext db, ICrmDirectory directory, ISender sender)
    {
        _db = db;
        _directory = directory;
        _sender = sender;
    }

    public async Task<DealDto> Handle(CreateDeal request, CancellationToken cancellationToken)
    {
        await DealRules.EnsureCompanyAndContactAsync(_directory, request.CompanyId, request.CustomerId, cancellationToken);

        var stageId = request.StageId
            ?? await _db.Stages.OrderBy(x => x.Position).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("The pipeline has no stages.", "stage.none");
        await StageExistsAsync(stageId, cancellationToken);

        var deal = new Deal
        {
            Title = request.Title.Trim(),
            CompanyId = request.CompanyId,
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            Currency = request.Currency,
            StageId = stageId,
            ExpectedCloseDate = request.ExpectedCloseDate
        };
        _db.Deals.Add(deal);
        await _db.SaveChangesAndPublishAsync(() => new DealCreated(deal.Id, deal.CompanyId, deal.Amount, deal.Currency), cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }

    private async Task StageExistsAsync(int stageId, CancellationToken cancellationToken)
    {
        if(!await _db.Stages.AnyAsync(x => x.Id == stageId, cancellationToken))
        {
            throw new NotFoundException("Stage", stageId);
        }
    }
}

/// <summary>Replaces the editable fields. Stage and status change through move / win / lose / reopen.</summary>
internal sealed record UpdateDeal(
    int Id,
    string Title,
    int CompanyId,
    int? CustomerId,
    decimal Amount,
    string Currency,
    DateOnly? ExpectedCloseDate) : ICommand<DealDto>;

internal sealed class UpdateDealValidator : AbstractValidator<UpdateDeal>
{
    public UpdateDealValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Currency).Must(Currencies.All.Contains)
            .WithMessage($"Supported currencies: {string.Join(", ", Currencies.All)}.");
    }
}

internal sealed class UpdateDealHandler : IRequestHandler<UpdateDeal, DealDto>
{
    private readonly SalesDbContext _db;
    private readonly ICrmDirectory _directory;
    private readonly ISender _sender;

    public UpdateDealHandler(SalesDbContext db, ICrmDirectory directory, ISender sender)
    {
        _db = db;
        _directory = directory;
        _sender = sender;
    }

    public async Task<DealDto> Handle(UpdateDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);

        // An inactive company may keep its existing deals; only switching to one is refused.
        await DealRules.EnsureCompanyAndContactAsync(_directory, request.CompanyId, request.CustomerId, cancellationToken,
            allowInactiveCompany: request.CompanyId == deal.CompanyId);

        deal.Title = request.Title.Trim();
        deal.CompanyId = request.CompanyId;
        deal.CustomerId = request.CustomerId;
        deal.Amount = request.Amount;
        deal.Currency = request.Currency;
        deal.ExpectedCloseDate = request.ExpectedCloseDate;
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }
}

internal sealed record DeleteDeal(int Id) : ICommand;

internal sealed class DeleteDealHandler : IRequestHandler<DeleteDeal, Unit>
{
    private readonly SalesDbContext _db;
    private readonly IEnumerable<IReferenceCheck> _referenceChecks;

    public DeleteDealHandler(SalesDbContext db, IEnumerable<IReferenceCheck> referenceChecks)
    {
        _db = db;
        _referenceChecks = referenceChecks;
    }

    public async Task<Unit> Handle(DeleteDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);
        await _referenceChecks.EnsureNotReferencedAsync(SalesResources.Deal, deal.Id, cancellationToken);

        _db.Deals.Remove(deal);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------- Pipeline: move / win / lose / reopen

internal sealed record MoveDeal(int Id, int StageId) : ICommand<DealDto>;

internal sealed class MoveDealHandler : IRequestHandler<MoveDeal, DealDto>
{
    private readonly SalesDbContext _db;
    private readonly ISender _sender;

    public MoveDealHandler(SalesDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<DealDto> Handle(MoveDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);
        DealRules.EnsureOpen(deal);

        if(!await _db.Stages.AnyAsync(x => x.Id == request.StageId, cancellationToken))
        {
            throw new NotFoundException("Stage", request.StageId);
        }

        deal.StageId = request.StageId;
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }
}

internal sealed record WinDeal(int Id) : ICommand<DealDto>;

internal sealed record LoseDeal(int Id, string? Reason) : ICommand<DealDto>;

internal sealed class LoseDealValidator : AbstractValidator<LoseDeal>
{
    public LoseDealValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

internal sealed record ReopenDeal(int Id) : ICommand<DealDto>;

internal sealed class CloseDealHandlers :
    IRequestHandler<WinDeal, DealDto>,
    IRequestHandler<LoseDeal, DealDto>,
    IRequestHandler<ReopenDeal, DealDto>
{
    private readonly SalesDbContext _db;
    private readonly ISender _sender;
    private readonly TimeProvider _clock;
    private readonly ICurrentUser _currentUser;

    public CloseDealHandlers(SalesDbContext db, ISender sender, TimeProvider clock, ICurrentUser currentUser)
    {
        _db = db;
        _sender = sender;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<DealDto> Handle(WinDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);
        DealRules.EnsureOpen(deal);

        deal.Status = DealStatus.Won;
        deal.ClosedAt = _clock.GetUtcNow().UtcDateTime;
        _db.AddIntegrationEvent(new DealWon(deal.Id, deal.CompanyId, deal.Amount, deal.Currency, deal.Title, deal.CreatedBy, _currentUser.Id));
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }

    public async Task<DealDto> Handle(LoseDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);
        DealRules.EnsureOpen(deal);

        deal.Status = DealStatus.Lost;
        deal.ClosedAt = _clock.GetUtcNow().UtcDateTime;
        deal.LostReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        _db.AddIntegrationEvent(new DealLost(deal.Id, deal.CompanyId, deal.LostReason, deal.Title, deal.CreatedBy, _currentUser.Id));
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }

    public async Task<DealDto> Handle(ReopenDeal request, CancellationToken cancellationToken)
    {
        var deal = await DealRules.FindAsync(_db, request.Id, cancellationToken);

        if(deal.Status == DealStatus.Open)
        {
            throw new BusinessRuleException("The deal is already open.", "deal.already_open");
        }

        deal.Status = DealStatus.Open;
        deal.ClosedAt = null;
        deal.LostReason = null;
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetDeal(deal.Id), cancellationToken);
    }
}

internal static class DealRules
{
    public static async Task<Deal> FindAsync(SalesDbContext db, int id, CancellationToken cancellationToken) =>
        await db.Deals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Deal", id);

    public static void EnsureOpen(Deal deal)
    {
        if(deal.Status != DealStatus.Open)
        {
            throw new BusinessRuleException("The deal is closed. Reopen it first.", "deal.closed");
        }
    }

    /// <summary>The company must exist (and be active), the contact must be one of its customers.</summary>
    public static async Task EnsureCompanyAndContactAsync(
        ICrmDirectory directory, int companyId, int? customerId, CancellationToken cancellationToken, bool allowInactiveCompany = false)
    {
        var company = (await directory.GetCompaniesAsync([companyId], cancellationToken)).GetValueOrDefault(companyId)
            ?? throw new NotFoundException("Company", companyId);

        if(!company.IsActive && !allowInactiveCompany)
        {
            throw new BusinessRuleException("Deals cannot be added for an inactive company.", "deal.company_inactive");
        }

        if(customerId is { } id)
        {
            var customer = (await directory.GetCustomersAsync([id], cancellationToken)).GetValueOrDefault(id)
                ?? throw new NotFoundException("Customer", id);

            if(customer.CompanyId != companyId)
            {
                throw new BusinessRuleException("The contact must be a customer of the deal's company.", "deal.contact_not_in_company");
            }
        }
    }
}
