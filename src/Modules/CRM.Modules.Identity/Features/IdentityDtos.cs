using System.Linq.Expressions;
using CRM.Modules.Identity.Domain;

namespace CRM.Modules.Identity.Features;

internal sealed record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>EF-translatable projection, so list queries only select the needed columns.</summary>
    public static readonly Expression<Func<User, UserDto>> Projection = u => new UserDto(
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.IsActive,
        u.PreferredLanguage,
        u.UserRoles.Select(r => r.Role.Name).OrderBy(n => n).ToList(),
        u.CreatedAt,
        u.UpdatedAt);

    private static readonly Func<User, UserDto> Compiled = Projection.Compile();

    /// <summary>Maps an entity whose <c>UserRoles.Role</c> navigation is loaded.</summary>
    public static UserDto From(User user) => Compiled(user);
}

/// <summary>The signed-in user plus their effective permissions (what the UI uses to show/hide features).</summary>
internal sealed record CurrentUserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CurrentUserDto From(UserDto user, IReadOnlyList<string> permissions) => new(
        user.Id, user.FirstName, user.LastName, user.Email, user.IsActive, user.PreferredLanguage,
        user.Roles, permissions, user.CreatedAt, user.UpdatedAt);
}

/// <param name="Permissions">For Admin every permission; the role's own assignments are not editable.</param>
internal sealed record RoleDto(
    int Id,
    string Name,
    bool IsActive,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt)
{
    public static readonly Expression<Func<Role, RoleDto>> Projection = r => new RoleDto(
        r.Id,
        r.Name,
        r.IsActive,
        r.Name == SystemRoles.Admin || r.Name == SystemRoles.User,
        r.UserRoles.Count,
        r.Permissions.Select(p => p.PermissionName).OrderBy(p => p).ToList(),
        r.CreatedAt);

    public RoleDto WithAdminPermissions(IReadOnlyList<string> all) =>
        Name == SystemRoles.Admin ? this with { Permissions = all } : this;
}

internal sealed record PermissionDto(string Name, string Module, string Group, string Description);

internal sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    CurrentUserDto User);
