namespace CRM.Core.Security;

/// <summary>Built-in role names. Use these in [Authorize(Roles = ...)] instead of string literals.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>Comma-separated, for attributes that accept any of several roles.</summary>
    public const string AdminOrUser = Admin + "," + User;

    public static readonly IReadOnlyList<string> All = [Admin, User];
}
