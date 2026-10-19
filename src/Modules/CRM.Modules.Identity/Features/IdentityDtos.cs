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

internal sealed record RoleDto(int Id, string Name, bool IsActive, bool IsSystem, int UserCount, DateTime CreatedAt)
{
    public static readonly Expression<Func<Role, RoleDto>> Projection = r => new RoleDto(
        r.Id,
        r.Name,
        r.IsActive,
        r.Name == SystemRoles.Admin || r.Name == SystemRoles.User,
        r.UserRoles.Count,
        r.CreatedAt);
}

internal sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
