using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Identity.Domain;

internal sealed class User : SoftDeletableEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [NotAudited]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>UI language of the user (<see cref="Languages"/>).</summary>
    public string PreferredLanguage { get; set; } = Languages.Default;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}";
}

internal sealed class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

internal sealed class UserRole
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}

internal sealed class RefreshToken : Entity
{
    public int UserId { get; set; }

    /// <summary>SHA-256 of the token; the token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public User User { get; set; } = null!;
}

/// <summary>Built-in roles that always exist and cannot be renamed or deleted.</summary>
internal static class SystemRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, User];

    public static bool IsSystemRole(string name) => All.Contains(name, StringComparer.OrdinalIgnoreCase);
}

internal static class Languages
{
    public const string Turkish = "tr";
    public const string English = "en";
    public const string Default = Turkish;

    public static readonly IReadOnlyList<string> Supported = [Turkish, English];
}
