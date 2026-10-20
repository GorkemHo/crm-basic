using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Tasks.Domain;

/// <summary>
/// A to-do assigned to a user, optionally about a company (and a contact or deal of it).
/// Named TaskItem to stay clear of <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
internal sealed class TaskItem : SoftDeletableEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>A calendar day; "overdue" and "today" are decided in the user's time zone by the caller.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>One of <see cref="TaskPriorities.All"/>.</summary>
    public string Priority { get; set; } = TaskPriorities.Normal;

    /// <summary>Identity user who should do it.</summary>
    public int AssigneeId { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CompletedBy { get; set; }

    public bool IsDone => CompletedAt.HasValue;

    public int? CompanyId { get; set; }

    public int? CustomerId { get; set; }

    public int? DealId { get; set; }
}

internal static class TaskPriorities
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";

    public static readonly string[] All = [Low, Normal, High];
}

internal static class TaskStates
{
    public const string Open = "open";
    public const string Done = "done";
}
