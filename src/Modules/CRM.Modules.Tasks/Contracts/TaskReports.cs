namespace CRM.Modules.Tasks.Contracts;

/// <param name="Open">Open now.</param>
/// <param name="Overdue">Open now and due before the given day.</param>
/// <param name="Completed">Completed in the period.</param>
public sealed record AssigneeTasks(int AssigneeId, int Open, int Overdue, int Completed);

public sealed record TaskReport(IReadOnlyList<AssigneeTasks> ByAssignee, int Open, int Overdue, int Completed);

/// <summary>Task figures for the Reporting module.</summary>
public interface ITaskReports
{
    /// <param name="today">The caller's calendar day, for "overdue".</param>
    Task<TaskReport> GetAsync(DateTime fromUtc, DateTime toUtc, DateOnly today, CancellationToken cancellationToken);
}
