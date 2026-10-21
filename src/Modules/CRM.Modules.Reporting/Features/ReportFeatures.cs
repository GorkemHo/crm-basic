using CRM.BuildingBlocks.Application;
using CRM.Modules.Activities.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Tasks.Contracts;
using FluentValidation;

namespace CRM.Modules.Reporting.Features;

/// <summary>
/// A reporting period of whole days, both ends included. Defaults: the last six calendar months up to today (UTC).
/// Months and days are counted in UTC.
/// </summary>
internal abstract class PeriodQuery
{
    public const int MaxDays = 3 * 366;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public (DateTime FromUtc, DateTime ToUtc) Range(DateOnly today)
    {
        var to = To ?? today;
        var from = From ?? new DateOnly(to.Year, to.Month, 1).AddMonths(-5);
        return (from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
}

internal sealed class PeriodValidator : AbstractValidator<PeriodQuery>
{
    public PeriodValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("The end of the period must not be before its start.");
        RuleFor(x => x)
            .Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber <= PeriodQuery.MaxDays)
            .When(x => x.From.HasValue && x.To.HasValue && x.To >= x.From)
            .OverridePropertyName(nameof(PeriodQuery.To))
            .WithMessage("The period can be at most three years.");
    }
}

// ---------------------------------------------------------------- Sales and pipeline

internal sealed class GetSalesReport : PeriodQuery, IQuery<SalesReport>;

internal sealed class GetSalesReportValidator : AbstractValidator<GetSalesReport>
{
    public GetSalesReportValidator() => Include(new PeriodValidator());
}

internal sealed record GetPipelineReport : IQuery<IReadOnlyList<PipelineStageReport>>;

// ---------------------------------------------------------------- Activities and tasks (with people's names)

internal sealed record UserActivitiesDto(int? UserId, string? UserName, int Total, IReadOnlyList<ActivityTypeCount> Types);

internal sealed record ActivityReportDto(IReadOnlyList<MonthlyActivities> Months, IReadOnlyList<UserActivitiesDto> ByUser, int Total);

internal sealed class GetActivityReport : PeriodQuery, IQuery<ActivityReportDto>;

internal sealed class GetActivityReportValidator : AbstractValidator<GetActivityReport>
{
    public GetActivityReportValidator() => Include(new PeriodValidator());
}

internal sealed record AssigneeTasksDto(int AssigneeId, string? AssigneeName, int Open, int Overdue, int Completed);

internal sealed record TaskReportDto(IReadOnlyList<AssigneeTasksDto> ByAssignee, int Open, int Overdue, int Completed);

/// <summary><see cref="Today"/> is the caller's calendar day, for "overdue" (defaults to today in UTC).</summary>
internal sealed class GetTaskReport : PeriodQuery, IQuery<TaskReportDto>
{
    public DateOnly? Today { get; set; }
}

internal sealed class GetTaskReportValidator : AbstractValidator<GetTaskReport>
{
    public GetTaskReportValidator() => Include(new PeriodValidator());
}

internal sealed class ReportHandlers :
    IRequestHandler<GetSalesReport, SalesReport>,
    IRequestHandler<GetPipelineReport, IReadOnlyList<PipelineStageReport>>,
    IRequestHandler<GetActivityReport, ActivityReportDto>,
    IRequestHandler<GetTaskReport, TaskReportDto>
{
    private readonly ISalesReports _sales;
    private readonly IActivityReports _activities;
    private readonly ITaskReports _tasks;
    private readonly IUserDirectory _users;
    private readonly TimeProvider _clock;

    public ReportHandlers(ISalesReports sales, IActivityReports activities, ITaskReports tasks, IUserDirectory users, TimeProvider clock)
    {
        _sales = sales;
        _activities = activities;
        _tasks = tasks;
        _users = users;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    public Task<SalesReport> Handle(GetSalesReport request, CancellationToken cancellationToken)
    {
        var (from, to) = request.Range(Today);
        return _sales.GetSalesAsync(from, to, cancellationToken);
    }

    public Task<IReadOnlyList<PipelineStageReport>> Handle(GetPipelineReport request, CancellationToken cancellationToken) =>
        _sales.GetPipelineAsync(cancellationToken);

    public async Task<ActivityReportDto> Handle(GetActivityReport request, CancellationToken cancellationToken)
    {
        var (from, to) = request.Range(Today);
        var report = await _activities.GetAsync(from, to, cancellationToken);
        var names = await _users.GetNamesAsync(report.ByUser.Where(u => u.UserId.HasValue).Select(u => u.UserId!.Value), cancellationToken);

        return new ActivityReportDto(
            report.Months,
            report.ByUser.Select(u => new UserActivitiesDto(u.UserId, u.UserId is { } id ? names.GetValueOrDefault(id) : null, u.Total, u.Types)).ToList(),
            report.Total);
    }

    public async Task<TaskReportDto> Handle(GetTaskReport request, CancellationToken cancellationToken)
    {
        var today = request.Today ?? Today;
        var (from, to) = request.Range(today);
        var report = await _tasks.GetAsync(from, to, today, cancellationToken);
        var names = await _users.GetNamesAsync(report.ByAssignee.Select(a => a.AssigneeId), cancellationToken);

        return new TaskReportDto(
            report.ByAssignee.Select(a => new AssigneeTasksDto(a.AssigneeId, names.GetValueOrDefault(a.AssigneeId), a.Open, a.Overdue, a.Completed)).ToList(),
            report.Open,
            report.Overdue,
            report.Completed);
    }
}
