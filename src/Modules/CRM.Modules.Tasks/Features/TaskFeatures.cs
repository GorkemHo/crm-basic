using System.Linq.Expressions;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Tasks.Contracts;
using CRM.Modules.Tasks.Domain;
using CRM.Modules.Tasks.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Tasks.Features;

/// <param name="AssigneeName">Names of records owned by other modules; null if they can no longer be found.</param>
internal sealed record TaskDto(
    int Id,
    string Title,
    string? Description,
    DateOnly? DueDate,
    string Priority,
    int AssigneeId,
    string? AssigneeName,
    bool IsDone,
    DateTime? CompletedAt,
    int? CompanyId,
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
    /// <summary>Names from other modules are filled in by <see cref="TaskNames"/>.</summary>
    public static readonly Expression<Func<TaskItem, TaskDto>> Projection = t => new TaskDto(
        t.Id, t.Title, t.Description, t.DueDate, t.Priority, t.AssigneeId, null, t.CompletedAt != null, t.CompletedAt,
        t.CompanyId, null, t.CustomerId, null, t.DealId, null, t.CreatedBy, null, t.CreatedAt, t.UpdatedAt);
}

/// <summary>Adds assignee, author, company, contact and deal names (owned by other modules) to tasks.</summary>
internal sealed class TaskNames
{
    private readonly ICrmDirectory _crm;
    private readonly ISalesDirectory _sales;
    private readonly IUserDirectory _users;

    public TaskNames(ICrmDirectory crm, ISalesDirectory sales, IUserDirectory users)
    {
        _crm = crm;
        _sales = sales;
        _users = users;
    }

    public async Task<IReadOnlyList<TaskDto>> FillAsync(IReadOnlyList<TaskDto> tasks, CancellationToken cancellationToken)
    {
        if(tasks.Count == 0)
        {
            return tasks;
        }

        var companies = await _crm.GetCompaniesAsync(Ids(tasks, t => t.CompanyId), cancellationToken);
        var customers = await _crm.GetCustomersAsync(Ids(tasks, t => t.CustomerId), cancellationToken);
        var deals = await _sales.GetDealsAsync(Ids(tasks, t => t.DealId), cancellationToken);
        var users = await _users.GetNamesAsync(tasks.Select(t => t.AssigneeId).Concat(Ids(tasks, t => t.CreatedBy)), cancellationToken);

        return tasks.Select(t => t with
        {
            AssigneeName = users.GetValueOrDefault(t.AssigneeId),
            CreatedByName = t.CreatedBy is { } author ? users.GetValueOrDefault(author) : null,
            CompanyName = t.CompanyId is { } companyId ? companies.GetValueOrDefault(companyId)?.CompanyName : null,
            CustomerName = t.CustomerId is { } customerId ? customers.GetValueOrDefault(customerId)?.FullName : null,
            DealTitle = t.DealId is { } dealId ? deals.GetValueOrDefault(dealId)?.Title : null
        }).ToList();
    }

    public async Task<TaskDto> FillAsync(TaskDto task, CancellationToken cancellationToken) =>
        (await FillAsync([task], cancellationToken))[0];

    private static IEnumerable<int> Ids(IEnumerable<TaskDto> tasks, Func<TaskDto, int?> id) =>
        tasks.Select(id).Where(x => x.HasValue).Select(x => x!.Value);
}

internal static class TaskOrdering
{
    /// <summary>
    /// Work-list order: open before done, then by due date (no date last), higher priority first, oldest first.
    /// Done tasks: most recently completed first.
    /// </summary>
    public static IOrderedQueryable<TaskItem> OrderForWorkList(this IQueryable<TaskItem> query) => query
        .OrderBy(t => t.CompletedAt != null)
        .ThenByDescending(t => t.CompletedAt)
        .ThenBy(t => t.DueDate == null)
        .ThenBy(t => t.DueDate)
        .ThenBy(t => t.Priority == TaskPriorities.High ? 0 : t.Priority == TaskPriorities.Normal ? 1 : 2)
        .ThenBy(t => t.Id);
}

// ---------------------------------------------------------------- List / get

internal sealed class GetTasks : PaginationRequest, IQuery<PagedResponse<TaskDto>>
{
    /// <summary>Matches title or description (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary><c>open</c> or <c>done</c>.</summary>
    public string? Status { get; set; }

    public int? AssigneeId { get; set; }

    /// <summary>Only tasks assigned to the caller.</summary>
    public bool? Mine { get; set; }

    public int? CompanyId { get; set; }

    public int? CustomerId { get; set; }

    public int? DealId { get; set; }

    /// <summary>Due on or after this day.</summary>
    public DateOnly? DueFrom { get; set; }

    /// <summary>Due on or before this day (e.g. the caller's today for "overdue or due today").</summary>
    public DateOnly? DueTo { get; set; }
}

internal sealed class GetTasksHandler : IRequestHandler<GetTasks, PagedResponse<TaskDto>>
{
    private readonly TasksDbContext _db;
    private readonly TaskNames _names;
    private readonly ICurrentUser _currentUser;

    public GetTasksHandler(TasksDbContext db, TaskNames names, ICurrentUser currentUser)
    {
        _db = db;
        _names = names;
        _currentUser = currentUser;
    }

    public async Task<PagedResponse<TaskDto>> Handle(GetTasks request, CancellationToken cancellationToken)
    {
        var query = _db.Tasks.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Title, pattern) || EF.Functions.ILike(x.Description!, pattern));
        }

        query = request.Status switch
        {
            TaskStates.Open => query.Where(x => x.CompletedAt == null),
            TaskStates.Done => query.Where(x => x.CompletedAt != null),
            _ => query
        };

        if(request.Mine == true) query = query.Where(x => x.AssigneeId == _currentUser.Id);
        if(request.AssigneeId.HasValue) query = query.Where(x => x.AssigneeId == request.AssigneeId);
        if(request.CompanyId.HasValue) query = query.Where(x => x.CompanyId == request.CompanyId);
        if(request.CustomerId.HasValue) query = query.Where(x => x.CustomerId == request.CustomerId);
        if(request.DealId.HasValue) query = query.Where(x => x.DealId == request.DealId);
        if(request.DueFrom.HasValue) query = query.Where(x => x.DueDate >= request.DueFrom);
        if(request.DueTo.HasValue) query = query.Where(x => x.DueDate <= request.DueTo);

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderForWorkList();
        }

        var page = await query.ToPagedResponseAsync(request, TaskDto.Projection, cancellationToken);

        return new PagedResponse<TaskDto>
        {
            Items = await _names.FillAsync(page.Items, cancellationToken),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }
}

internal sealed record GetTask(int Id) : IQuery<TaskDto>;

internal sealed class GetTaskHandler : IRequestHandler<GetTask, TaskDto>
{
    private readonly TasksDbContext _db;
    private readonly TaskNames _names;

    public GetTaskHandler(TasksDbContext db, TaskNames names)
    {
        _db = db;
        _names = names;
    }

    public async Task<TaskDto> Handle(GetTask request, CancellationToken cancellationToken)
    {
        var task = await _db.Tasks.AsNoTracking().Where(x => x.Id == request.Id).Select(TaskDto.Projection).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Task", request.Id);

        return await _names.FillAsync(task, cancellationToken);
    }
}

internal sealed record AssigneeDto(int Id, string FullName);

/// <summary>Active users tasks can be assigned to (users don't need <c>identity.users.read</c> for this).</summary>
internal sealed record GetAssignees : IQuery<IReadOnlyList<AssigneeDto>>;

internal sealed class GetAssigneesHandler : IRequestHandler<GetAssignees, IReadOnlyList<AssigneeDto>>
{
    private readonly IUserDirectory _users;

    public GetAssigneesHandler(IUserDirectory users) => _users = users;

    public async Task<IReadOnlyList<AssigneeDto>> Handle(GetAssignees request, CancellationToken cancellationToken) =>
        (await _users.GetActiveUsersAsync(cancellationToken)).Select(u => new AssigneeDto(u.Id, u.FullName)).ToList();
}

internal sealed record MyTaskSummary(int Open, int Overdue, int DueToday);

/// <param name="Today">The caller's calendar day (defaults to today in UTC).</param>
internal sealed record GetMyTaskSummary(DateOnly? Today) : IQuery<MyTaskSummary>;

internal sealed class GetMyTaskSummaryHandler : IRequestHandler<GetMyTaskSummary, MyTaskSummary>
{
    private readonly TasksDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public GetMyTaskSummaryHandler(TasksDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MyTaskSummary> Handle(GetMyTaskSummary request, CancellationToken cancellationToken)
    {
        var today = request.Today ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var open = _db.Tasks.Where(x => x.AssigneeId == _currentUser.Id && x.CompletedAt == null);

        return new MyTaskSummary(
            await open.CountAsync(cancellationToken),
            await open.CountAsync(x => x.DueDate < today, cancellationToken),
            await open.CountAsync(x => x.DueDate == today, cancellationToken));
    }
}

// ---------------------------------------------------------------- Create / update / complete / delete

/// <summary>Fields of a task, shared by create and update.</summary>
internal interface ITaskFields
{
    string Title { get; }
    string? Description { get; }
    DateOnly? DueDate { get; }
    string Priority { get; }
    int? AssigneeId { get; }
    int? CompanyId { get; }
    int? CustomerId { get; }
    int? DealId { get; }
}

/// <param name="AssigneeId">Defaults to the caller.</param>
internal sealed record CreateTask(
    string Title, string? Description, DateOnly? DueDate, string Priority, int? AssigneeId,
    int? CompanyId, int? CustomerId, int? DealId) : ICommand<TaskDto>, ITaskFields;

/// <summary>Replaces every field (assignee included). Only the assignee, the creator or a manager may change a task.</summary>
internal sealed record UpdateTask(
    int Id, string Title, string? Description, DateOnly? DueDate, string Priority, int? AssigneeId,
    int? CompanyId, int? CustomerId, int? DealId) : ICommand<TaskDto>, ITaskFields;

internal sealed class TaskFieldsValidator : AbstractValidator<ITaskFields>
{
    public TaskFieldsValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority).Must(TaskPriorities.All.Contains)
            .WithMessage($"Supported priorities: {string.Join(", ", TaskPriorities.All)}.");
        RuleFor(x => x.CompanyId).NotNull()
            .When(x => x.CustomerId.HasValue || x.DealId.HasValue)
            .WithMessage("Choose the company of the contact or deal.");
    }
}

internal sealed class CreateTaskValidator : AbstractValidator<CreateTask>
{
    public CreateTaskValidator() => Include(new TaskFieldsValidator());
}

internal sealed class UpdateTaskValidator : AbstractValidator<UpdateTask>
{
    public UpdateTaskValidator() => Include(new TaskFieldsValidator());
}

internal sealed class TaskCommandHandlers :
    IRequestHandler<CreateTask, TaskDto>,
    IRequestHandler<UpdateTask, TaskDto>
{
    private readonly TasksDbContext _db;
    private readonly TaskRules _rules;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public TaskCommandHandlers(TasksDbContext db, TaskRules rules, ICurrentUser currentUser, ISender sender)
    {
        _db = db;
        _rules = rules;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<TaskDto> Handle(CreateTask request, CancellationToken cancellationToken)
    {
        var assigneeId = await _rules.ResolveAssigneeAsync(request.AssigneeId, currentAssigneeId: null, cancellationToken);
        await _rules.EnsureLinksAsync(request, cancellationToken);

        var task = new TaskItem { AssigneeId = assigneeId };
        Apply(task, request);
        _db.Tasks.Add(task);

        if(assigneeId != _currentUser.Id)
        {
            await _db.SaveChangesAndPublishAsync(() => new TaskAssigned(task.Id, assigneeId, _currentUser.Id, task.Title), cancellationToken);
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await _sender.Send(new GetTask(task.Id), cancellationToken);
    }

    public async Task<TaskDto> Handle(UpdateTask request, CancellationToken cancellationToken)
    {
        var task = await _rules.FindChangeableAsync(request.Id, cancellationToken);
        var assigneeId = await _rules.ResolveAssigneeAsync(request.AssigneeId, task.AssigneeId, cancellationToken);
        await _rules.EnsureLinksAsync(request, cancellationToken);

        var reassigned = assigneeId != task.AssigneeId;
        task.AssigneeId = assigneeId;
        Apply(task, request);

        if(reassigned && assigneeId != _currentUser.Id)
        {
            _db.AddIntegrationEvent(new TaskAssigned(task.Id, assigneeId, _currentUser.Id, task.Title));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetTask(task.Id), cancellationToken);
    }

    private static void Apply(TaskItem task, ITaskFields fields)
    {
        task.Title = fields.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(fields.Description) ? null : fields.Description.Trim();
        task.DueDate = fields.DueDate;
        task.Priority = fields.Priority;
        task.CompanyId = fields.CompanyId;
        task.CustomerId = fields.CustomerId;
        task.DealId = fields.DealId;
    }
}

internal sealed record CompleteTask(int Id) : ICommand<TaskDto>;

internal sealed record ReopenTask(int Id) : ICommand<TaskDto>;

internal sealed record DeleteTask(int Id) : ICommand;

internal sealed class TaskStateHandlers :
    IRequestHandler<CompleteTask, TaskDto>,
    IRequestHandler<ReopenTask, TaskDto>,
    IRequestHandler<DeleteTask, Unit>
{
    private readonly TasksDbContext _db;
    private readonly TaskRules _rules;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;
    private readonly ISender _sender;

    public TaskStateHandlers(TasksDbContext db, TaskRules rules, ICurrentUser currentUser, TimeProvider clock, ISender sender)
    {
        _db = db;
        _rules = rules;
        _currentUser = currentUser;
        _clock = clock;
        _sender = sender;
    }

    public async Task<TaskDto> Handle(CompleteTask request, CancellationToken cancellationToken)
    {
        var task = await _rules.FindChangeableAsync(request.Id, cancellationToken);

        if(task.IsDone)
        {
            throw new BusinessRuleException("The task is already done.", "task.already_done");
        }

        task.CompletedAt = _clock.GetUtcNow().UtcDateTime;
        task.CompletedBy = _currentUser.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetTask(task.Id), cancellationToken);
    }

    public async Task<TaskDto> Handle(ReopenTask request, CancellationToken cancellationToken)
    {
        var task = await _rules.FindChangeableAsync(request.Id, cancellationToken);

        if(!task.IsDone)
        {
            throw new BusinessRuleException("The task is already open.", "task.already_open");
        }

        task.CompletedAt = null;
        task.CompletedBy = null;
        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetTask(task.Id), cancellationToken);
    }

    public async Task<Unit> Handle(DeleteTask request, CancellationToken cancellationToken)
    {
        var task = await _rules.FindChangeableAsync(request.Id, cancellationToken);

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal sealed class TaskRules
{
    private readonly TasksDbContext _db;
    private readonly ICrmDirectory _crm;
    private readonly ISalesDirectory _sales;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _currentUser;

    public TaskRules(TasksDbContext db, ICrmDirectory crm, ISalesDirectory sales, IUserDirectory users, ICurrentUser currentUser)
    {
        _db = db;
        _crm = crm;
        _sales = sales;
        _users = users;
        _currentUser = currentUser;
    }

    /// <exception cref="ForbiddenException">The caller is not the assignee, the creator or a tasks manager.</exception>
    public async Task<TaskItem> FindChangeableAsync(int id, CancellationToken cancellationToken)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task", id);

        // No signed-in user means the system itself (startup seeding); every endpoint requires a permission.
        var system = !_currentUser.IsAuthenticated;
        var mine = _currentUser.Id is { } me && (task.AssigneeId == me || task.CreatedBy == me);
        if(!system && !mine && !_currentUser.HasPermission(TasksPermissions.Manage))
        {
            throw new ForbiddenException("Only the assignee or the creator can change this task.", "task.not_yours");
        }

        return task;
    }

    /// <summary>
    /// The requested assignee (or the caller when none is given) must be an active user.
    /// Keeping an already assigned user who was deactivated since is allowed.
    /// </summary>
    public async Task<int> ResolveAssigneeAsync(int? requested, int? currentAssigneeId, CancellationToken cancellationToken)
    {
        var assigneeId = requested ?? currentAssigneeId ?? _currentUser.Id
            ?? throw new CRM.BuildingBlocks.Exceptions.ValidationException("assigneeId", "Choose who the task is for.", "task.assignee_required");

        if(assigneeId == currentAssigneeId)
        {
            return assigneeId;
        }

        var user = (await _users.GetUsersAsync([assigneeId], cancellationToken)).GetValueOrDefault(assigneeId)
            ?? throw new NotFoundException("User", assigneeId);

        if(!user.IsActive)
        {
            throw new BusinessRuleException("Tasks cannot be assigned to an inactive user.", "task.assignee_inactive");
        }

        return assigneeId;
    }

    /// <summary>The company must exist; the contact and the deal must belong to it.</summary>
    public async Task EnsureLinksAsync(ITaskFields fields, CancellationToken cancellationToken)
    {
        if(fields.CompanyId is not { } companyId)
        {
            return;
        }

        if(!(await _crm.GetCompaniesAsync([companyId], cancellationToken)).ContainsKey(companyId))
        {
            throw new NotFoundException("Company", companyId);
        }

        if(fields.CustomerId is { } customerId)
        {
            var customer = (await _crm.GetCustomersAsync([customerId], cancellationToken)).GetValueOrDefault(customerId)
                ?? throw new NotFoundException("Customer", customerId);

            if(customer.CompanyId != companyId)
            {
                throw new BusinessRuleException("The contact must be a customer of the task's company.", "task.contact_not_in_company");
            }
        }

        if(fields.DealId is { } dealId)
        {
            var deal = (await _sales.GetDealsAsync([dealId], cancellationToken)).GetValueOrDefault(dealId)
                ?? throw new NotFoundException("Deal", dealId);

            if(deal.CompanyId != companyId)
            {
                throw new BusinessRuleException("The deal must belong to the task's company.", "task.deal_not_in_company");
            }
        }
    }
}
