using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Activities.Contracts;

/// <summary>Permissions checked by the Activities module.</summary>
public static class ActivitiesPermissions
{
    public const string Read = "activities.activities.read";

    /// <summary>Log activities, and edit or delete the ones you logged.</summary>
    public const string Write = "activities.activities.write";

    /// <summary>Edit or delete anyone's activities.</summary>
    public const string Manage = "activities.activities.manage";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Read, "Activities", "View notes, calls, meetings and emails", GrantedToUsersByDefault: true),
        new(Write, "Activities", "Log activities; edit and delete your own", GrantedToUsersByDefault: true),
        new(Manage, "Activities", "Edit and delete anyone's activities")
    ];
}
