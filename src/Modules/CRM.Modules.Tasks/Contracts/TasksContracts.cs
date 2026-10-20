using CRM.BuildingBlocks.Messaging;
using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Tasks.Contracts;

/// <summary>Permissions checked by the Tasks module.</summary>
public static class TasksPermissions
{
    public const string Read = "tasks.tasks.read";

    /// <summary>Create tasks (for anyone), and edit / complete / delete tasks you created or are assigned to.</summary>
    public const string Write = "tasks.tasks.write";

    /// <summary>Edit, complete and delete anyone's tasks.</summary>
    public const string Manage = "tasks.tasks.manage";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Read, "Tasks", "View tasks", GrantedToUsersByDefault: true),
        new(Write, "Tasks", "Create and assign tasks; change tasks you created or are assigned to", GrantedToUsersByDefault: true),
        new(Manage, "Tasks", "Change and delete anyone's tasks")
    ];
}

/// <summary>A task was given to someone other than the person who assigned it (on creation or reassignment).</summary>
public sealed record TaskAssigned(int TaskId, int AssigneeId, int? AssignedBy, string Title) : IntegrationEvent;
