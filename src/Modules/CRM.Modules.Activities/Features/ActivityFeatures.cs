using System.Linq.Expressions;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Activities.Contracts;
using CRM.Modules.Activities.Domain;
using CRM.Modules.Activities.Infrastructure.Persistence;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Activities.Features;

/// <param name="CompanyName">Names of records owned by other modules; null if they can no longer be found.</param>
/// <param name="CreatedByName">The author; null for activities created by the system (e.g. demo data).</param>
internal sealed record ActivityDto(
    int Id,
    string Type,
    string Subject,
    string? Notes,
    DateTime OccurredAt,
    int? DurationMinutes,
    int CompanyId,
    string? CompanyName,
    int? CustomerId,
    string? CustomerName,
    int? DealId,
    string? DealTitle,
    int? CreatedBy,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Names from other modules are filled in by <see cref="ActivityNames"/>.</summary>
    public static readonly Expression<Func<Activity, ActivityDto>> Projection = a => new ActivityDto(
        a.Id, a.Type, a.Subject, a.Notes, a.OccurredAt, a.DurationMinutes,
        a.CompanyId, null, a.CustomerId, null, a.DealId, null, a.CreatedBy, null, a.CreatedAt, a.UpdatedAt);
}

/// <summary>Adds company, contact, deal and author names (owned by other modules) to activities.</summary>
internal sealed class ActivityNames
{
    private readonly ICrmDirectory _crm;
    private readonly ISalesDirectory _sales;
    private readonly IUserDirectory _users;

    public ActivityNames(ICrmDirectory crm, ISalesDirectory sales, IUserDirectory users)
    {
        _crm = crm;
        _sales = sales;
        _users = users;
    }

    public async Task<IReadOnlyList<ActivityDto>> FillAsync(IReadOnlyList<ActivityDto> activities, CancellationToken cancellationToken)
    {
        if(activities.Count == 0)
        {
            return activities;
        }

        var companies = await _crm.GetCompaniesAsync(activities.Select(a => a.CompanyId), cancellationToken);
        var customers = await _crm.GetCustomersAsync(Ids(activities, a => a.CustomerId), cancellationToken);
        var deals = await _sales.GetDealsAsync(Ids(activities, a => a.DealId), cancellationToken);
        var users = await _users.GetNamesAsync(Ids(activities, a => a.CreatedBy), cancellationToken);

        return activities.Select(a => a with
        {
            CompanyName = companies.GetValueOrDefault(a.CompanyId)?.CompanyName,
            CustomerName = a.CustomerId is { } customerId ? customers.GetValueOrDefault(customerId)?.FullName : null,
            DealTitle = a.DealId is { } dealId ? deals.GetValueOrDefault(dealId)?.Title : null,
            CreatedByName = a.CreatedBy is { } userId ? users.GetValueOrDefault(userId) : null
        }).ToList();
    }

    public async Task<ActivityDto> FillAsync(ActivityDto activity, CancellationToken cancellationToken) =>
        (await FillAsync([activity], cancellationToken))[0];

    private static IEnumerable<int> Ids(IEnumerable<ActivityDto> activities, Func<ActivityDto, int?> id) =>
        activities.Select(id).Where(x => x.HasValue).Select(x => x!.Value);
}

// ---------------------------------------------------------------- List / get

/// <summary>Newest first by default. <see cref="CompanyId"/> includes activities about the company's contacts and deals.</summary>
internal sealed class GetActivities : PaginationRequest, IQuery<PagedResponse<ActivityDto>>
{
    /// <summary>Matches subject or notes (case-insensitive).</summary>
    public string? Search { get; set; }

    public string? Type { get; set; }

    public int? CompanyId { get; set; }

    public int? CustomerId { get; set; }

    public int? DealId { get; set; }

    /// <summary>Only activities logged by this user.</summary>
    public int? CreatedBy { get; set; }
}

internal sealed class GetActivitiesHandler : IRequestHandler<GetActivities, PagedResponse<ActivityDto>>
{
    private readonly ActivitiesDbContext _db;
    private readonly ActivityNames _names;

    public GetActivitiesHandler(ActivitiesDbContext db, ActivityNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<PagedResponse<ActivityDto>> Handle(GetActivities request, CancellationToken cancellationToken)
    {
        var query = _db.Activities.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Subject, pattern) || EF.Functions.ILike(x.Notes!, pattern));
        }

        if(!string.IsNullOrWhiteSpace(request.Type)) query = query.Where(x => x.Type == request.Type);
        if(request.CompanyId.HasValue) query = query.Where(x => x.CompanyId == request.CompanyId);
        if(request.CustomerId.HasValue) query = query.Where(x => x.CustomerId == request.CustomerId);
        if(request.DealId.HasValue) query = query.Where(x => x.DealId == request.DealId);
        if(request.CreatedBy.HasValue) query = query.Where(x => x.CreatedBy == request.CreatedBy);

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id);
        }

        var page = await query.ToPagedResponseAsync(request, ActivityDto.Projection, cancellationToken);

        return new PagedResponse<ActivityDto>
        {
            Items = await _names.FillAsync(page.Items, cancellationToken),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }
}

internal sealed record GetActivity(int Id) : IQuery<ActivityDto>;

internal sealed class GetActivityHandler : IRequestHandler<GetActivity, ActivityDto>
{
    private readonly ActivitiesDbContext _db;
    private readonly ActivityNames _names;

    public GetActivityHandler(ActivitiesDbContext db, ActivityNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<ActivityDto> Handle(GetActivity request, CancellationToken cancellationToken)
    {
        var activity = await _db.Activities.AsNoTracking().Where(x => x.Id == request.Id).Select(ActivityDto.Projection).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Activity", request.Id);

        return await _names.FillAsync(activity, cancellationToken);
    }
}

// ---------------------------------------------------------------- Log / edit / delete

/// <summary>Fields of an activity, shared by log and edit.</summary>
internal interface IActivityFields
{
    string Type { get; }
    string Subject { get; }
    string? Notes { get; }
    DateTime OccurredAt { get; }
    int? DurationMinutes { get; }
    int CompanyId { get; }
    int? CustomerId { get; }
    int? DealId { get; }
}

internal sealed record LogActivity(
    string Type, string Subject, string? Notes, DateTime OccurredAt, int? DurationMinutes,
    int CompanyId, int? CustomerId, int? DealId) : ICommand<ActivityDto>, IActivityFields;

/// <summary>Replaces every field. Only the author, or someone with <see cref="ActivitiesPermissions.Manage"/>, may edit.</summary>
internal sealed record UpdateActivity(
    int Id, string Type, string Subject, string? Notes, DateTime OccurredAt, int? DurationMinutes,
    int CompanyId, int? CustomerId, int? DealId) : ICommand<ActivityDto>, IActivityFields;

internal sealed class ActivityFieldsValidator : AbstractValidator<IActivityFields>
{
    public ActivityFieldsValidator()
    {
        RuleFor(x => x.Type).Must(ActivityTypes.All.Contains)
            .WithMessage($"Supported types: {string.Join(", ", ActivityTypes.All)}.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.OccurredAt).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, 24 * 60);
        RuleFor(x => x.CompanyId).GreaterThan(0);
    }
}

internal sealed class LogActivityValidator : AbstractValidator<LogActivity>
{
    public LogActivityValidator() => Include(new ActivityFieldsValidator());
}

internal sealed class UpdateActivityValidator : AbstractValidator<UpdateActivity>
{
    public UpdateActivityValidator() => Include(new ActivityFieldsValidator());
}

internal sealed class ActivityCommandHandlers :
    IRequestHandler<LogActivity, ActivityDto>,
    IRequestHandler<UpdateActivity, ActivityDto>
{
    private readonly ActivitiesDbContext _db;
    private readonly ActivityRules _rules;
    private readonly ISender _sender;

    public ActivityCommandHandlers(ActivitiesDbContext db, ActivityRules rules, ISender sender)
    {
        _db = db;
        _rules = rules;
        _sender = sender;
    }

    public async Task<ActivityDto> Handle(LogActivity request, CancellationToken cancellationToken)
    {
        await _rules.EnsureLinksAsync(request, cancellationToken);

        var activity = new Activity();
        Apply(activity, request);
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetActivity(activity.Id), cancellationToken);
    }

    public async Task<ActivityDto> Handle(UpdateActivity request, CancellationToken cancellationToken)
    {
        var activity = await _rules.FindEditableAsync(request.Id, cancellationToken);
        await _rules.EnsureLinksAsync(request, cancellationToken);

        Apply(activity, request);
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetActivity(activity.Id), cancellationToken);
    }

    private static void Apply(Activity activity, IActivityFields fields)
    {
        activity.Type = fields.Type;
        activity.Subject = fields.Subject.Trim();
        activity.Notes = string.IsNullOrWhiteSpace(fields.Notes) ? null : fields.Notes.Trim();
        activity.OccurredAt = DateTime.SpecifyKind(fields.OccurredAt.ToUniversalTime(), DateTimeKind.Utc);
        activity.DurationMinutes = fields.Type is ActivityTypes.Call or ActivityTypes.Meeting ? fields.DurationMinutes : null;
        activity.CompanyId = fields.CompanyId;
        activity.CustomerId = fields.CustomerId;
        activity.DealId = fields.DealId;
    }
}

/// <summary>Only the author, or someone with <see cref="ActivitiesPermissions.Manage"/>, may delete.</summary>
internal sealed record DeleteActivity(int Id) : ICommand;

internal sealed class DeleteActivityHandler : IRequestHandler<DeleteActivity, Unit>
{
    private readonly ActivitiesDbContext _db;
    private readonly ActivityRules _rules;

    public DeleteActivityHandler(ActivitiesDbContext db, ActivityRules rules)
    {
        _db = db;
        _rules = rules;
    }

    public async Task<Unit> Handle(DeleteActivity request, CancellationToken cancellationToken)
    {
        var activity = await _rules.FindEditableAsync(request.Id, cancellationToken);

        _db.Activities.Remove(activity);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal sealed class ActivityRules
{
    private readonly ActivitiesDbContext _db;
    private readonly ICrmDirectory _crm;
    private readonly ISalesDirectory _sales;
    private readonly ICurrentUser _currentUser;

    public ActivityRules(ActivitiesDbContext db, ICrmDirectory crm, ISalesDirectory sales, ICurrentUser currentUser)
    {
        _db = db;
        _crm = crm;
        _sales = sales;
        _currentUser = currentUser;
    }

    /// <exception cref="ForbiddenException">The caller is neither the author nor an activities manager.</exception>
    public async Task<Activity> FindEditableAsync(int id, CancellationToken cancellationToken)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Activity", id);

        // No signed-in user means the system itself (startup seeding); every endpoint requires a permission.
        var system = !_currentUser.IsAuthenticated;
        if(!system && activity.CreatedBy != _currentUser.Id && !_currentUser.HasPermission(ActivitiesPermissions.Manage))
        {
            throw new ForbiddenException("Only the author can change this activity.", "activity.not_author");
        }

        return activity;
    }

    /// <summary>The company must exist; the contact and the deal must belong to it.</summary>
    public async Task EnsureLinksAsync(IActivityFields fields, CancellationToken cancellationToken)
    {
        if(!(await _crm.GetCompaniesAsync([fields.CompanyId], cancellationToken)).ContainsKey(fields.CompanyId))
        {
            throw new NotFoundException("Company", fields.CompanyId);
        }

        if(fields.CustomerId is { } customerId)
        {
            var customer = (await _crm.GetCustomersAsync([customerId], cancellationToken)).GetValueOrDefault(customerId)
                ?? throw new NotFoundException("Customer", customerId);

            if(customer.CompanyId != fields.CompanyId)
            {
                throw new BusinessRuleException("The contact must be a customer of the activity's company.", "activity.contact_not_in_company");
            }
        }

        if(fields.DealId is { } dealId)
        {
            var deal = (await _sales.GetDealsAsync([dealId], cancellationToken)).GetValueOrDefault(dealId)
                ?? throw new NotFoundException("Deal", dealId);

            if(deal.CompanyId != fields.CompanyId)
            {
                throw new BusinessRuleException("The deal must belong to the activity's company.", "activity.deal_not_in_company");
            }
        }
    }
}
