namespace CRM.BuildingBlocks.Security;

/// <summary>
/// A permission a module exposes, e.g. <c>crm.companies.write</c>.
/// Names follow <c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>.
/// </summary>
/// <param name="Name">Unique, stable name stored in role assignments and tokens.</param>
/// <param name="Group">Grouping shown in the role editor (usually the resource).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="GrantedToUsersByDefault">Whether the built-in <c>User</c> role gets it when first seeded.</param>
public sealed record PermissionDefinition(
    string Name,
    string Group,
    string Description,
    bool GrantedToUsersByDefault = false);
