using CRM.Modules.Tasks.Domain;
using CRM.Modules.Tasks.Features;

namespace CRM.UnitTests.Tasks;

public class TaskOrderingTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void WorkList_OpenFirst_ByDueDate_NoDateLast_ThenPriority()
    {
        TaskItem[] tasks =
        [
            Task(1, "no date", null, TaskPriorities.High),
            Task(2, "later", Today.AddDays(5), TaskPriorities.Normal),
            Task(3, "today low", Today, TaskPriorities.Low),
            Task(4, "today high", Today, TaskPriorities.High),
            Task(5, "overdue", Today.AddDays(-1), TaskPriorities.Low),
            Task(6, "done earlier", Today.AddDays(-3), TaskPriorities.High, completedAt: new DateTime(2026, 10, 1)),
            Task(7, "done recently", null, TaskPriorities.Low, completedAt: new DateTime(2026, 10, 19)),
        ];

        var ordered = tasks.AsQueryable().OrderForWorkList().Select(t => t.Title).ToList();

        Assert.Equal(["overdue", "today high", "today low", "later", "no date", "done recently", "done earlier"], ordered);
    }

    [Fact]
    public void WorkList_SameDueAndPriority_OldestFirst()
    {
        TaskItem[] tasks = [Task(9, "second", Today, TaskPriorities.Normal), Task(8, "first", Today, TaskPriorities.Normal)];

        Assert.Equal(["first", "second"], tasks.AsQueryable().OrderForWorkList().Select(t => t.Title));
    }

    private static TaskItem Task(int id, string title, DateOnly? due, string priority, DateTime? completedAt = null) =>
        new() { Id = id, Title = title, DueDate = due, Priority = priority, CompletedAt = completedAt, AssigneeId = 1 };
}
