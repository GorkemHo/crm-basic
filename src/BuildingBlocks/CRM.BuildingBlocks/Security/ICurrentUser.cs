namespace CRM.BuildingBlocks.Security;

/// <summary>
/// The authenticated caller of the current request. Inject it wherever user context is needed
/// (auditing, ownership checks, per-user data) instead of reading claims directly.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Null when the request is anonymous.</summary>
    int? Id { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    /// <summary>Permissions granted by the access token (<c>perm</c> claims).</summary>
    IReadOnlyCollection<string> Permissions { get; }

    bool IsInRole(string role);

    bool HasPermission(string permission);
}
