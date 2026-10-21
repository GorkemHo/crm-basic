using CRM.Modules.Tasks.Contracts;
using CRM.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Tasks.Infrastructure;

internal sealed class TaskReports : ITaskReports
{
    private readonly TasksDbContext _db;

    public TaskReports(TasksDbContext db) => _db = db;

    public async Task<TaskReport> GetAsync(DateTime fromUtc, DateTime toUtc, DateOnly today, CancellationToken cancellationToken)
    {
        var open = await _db.Tasks.AsNoTracking()
            .Where(x => x.CompletedAt == null)
            .GroupBy(x => x.AssigneeId)
            .Select(g => new { AssigneeId = g.Key, Open = g.Count(), Overdue = g.Count(x => x.DueDate < today) })
            .ToListAsync(cancellationToken);

        var completed = await _db.Tasks.AsNoTracking()
            .Where(x => x.CompletedAt >= fromUtc && x.CompletedAt < toUtc)
            .GroupBy(x => x.AssigneeId)
            .Select(g => new { AssigneeId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byAssignee = open.Select(o => o.AssigneeId).Union(completed.Select(c => c.AssigneeId))
            .Select(id =>
            {
                var o = open.FirstOrDefault(x => x.AssigneeId == id);
                return new AssigneeTasks(id, o?.Open ?? 0, o?.Overdue ?? 0, completed.FirstOrDefault(x => x.AssigneeId == id)?.Count ?? 0);
            })
            .OrderByDescending(a => a.Overdue).ThenByDescending(a => a.Open).ThenBy(a => a.AssigneeId)
            .ToList();

        return new TaskReport(byAssignee, byAssignee.Sum(a => a.Open), byAssignee.Sum(a => a.Overdue), byAssignee.Sum(a => a.Completed));
    }
}
