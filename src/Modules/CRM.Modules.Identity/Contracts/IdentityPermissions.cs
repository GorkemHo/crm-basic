using CRM.BuildingBlocks.Security;

namespace CRM.Modules.Identity.Contracts;

/// <summary>Permissions checked by the Identity module.</summary>
public static class IdentityPermissions
{
    public const string UsersRead = "identity.users.read";
    public const string UsersManage = "identity.users.manage";
    public const string RolesRead = "identity.roles.read";
    public const string RolesManage = "identity.roles.manage";

    internal static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(UsersRead, "Users", "View users"),
        new(UsersManage, "Users", "Create, edit and delete users; assign roles"),
        new(RolesRead, "Roles", "View roles and permissions"),
        new(RolesManage, "Roles", "Create, edit and delete roles; change role permissions")
    ];
}
