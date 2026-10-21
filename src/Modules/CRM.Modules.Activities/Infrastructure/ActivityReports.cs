using CRM.BuildingBlocks.Reporting;
using CRM.Modules.Activities.Contracts;
using CRM.Modules.Activities.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Activities.Infrastructure;

internal sealed class ActivityReports : IActivityReports
{
    private readonly ActivitiesDbContext _db;

    public ActivityReports(ActivitiesDbContext db) => _db = db;

    public async Task<ActivityReport> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var counts = await _db.Activities.AsNoTracking()
            .Where(x => x.OccurredAt >= fromUtc && x.OccurredAt < toUtc)
            .GroupBy(x => new { x.OccurredAt.Year, x.OccurredAt.Month, x.Type, x.CreatedBy })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, g.Key.CreatedBy, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var months = ReportMonths.Between(fromUtc, toUtc)
            .Select(m => new MonthlyActivities(m.Year, m.Month, Types(counts.Where(c => c.Year == m.Year && c.Month == m.Month)
                .Select(c => (c.Type, c.Count)))))
            .ToList();

        var byUser = counts
            .GroupBy(c => c.CreatedBy)
            .Select(g => new UserActivities(g.Key, g.Sum(c => c.Count), Types(g.Select(c => (c.Type, c.Count)))))
            .OrderByDescending(u => u.Total)
            .ToList();

        return new ActivityReport(months, byUser, counts.Sum(c => c.Count));
    }

    private static IReadOnlyList<ActivityTypeCount> Types(IEnumerable<(string Type, int Count)> counts) =>
        counts.GroupBy(c => c.Type).OrderBy(g => g.Key).Select(g => new ActivityTypeCount(g.Key, g.Sum(c => c.Count))).ToList();
}
