namespace CRM.Modules.Activities.Contracts;

/// <summary>Activities per type (note, call, meeting, email).</summary>
public sealed record ActivityTypeCount(string Type, int Count);

public sealed record MonthlyActivities(int Year, int Month, IReadOnlyList<ActivityTypeCount> Types);

/// <param name="UserId">Author; null for activities logged by the system.</param>
public sealed record UserActivities(int? UserId, int Total, IReadOnlyList<ActivityTypeCount> Types);

public sealed record ActivityReport(IReadOnlyList<MonthlyActivities> Months, IReadOnlyList<UserActivities> ByUser, int Total);

/// <summary>Activity figures for the Reporting module.</summary>
public interface IActivityReports
{
    /// <summary>Activities that happened in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<ActivityReport> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
