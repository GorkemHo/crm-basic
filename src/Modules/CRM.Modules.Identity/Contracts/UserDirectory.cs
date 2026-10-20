namespace CRM.Modules.Identity.Contracts;

public sealed record UserSummary(int Id, string FullName, string Email, bool IsActive);

/// <summary>Read-only user lookups for other modules (authors, assignees).</summary>
public interface IUserDirectory
{
    /// <summary>Full names by user id; deleted users are included so old records still show their author.</summary>
    Task<IReadOnlyDictionary<int, string>> GetNamesAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    /// <summary>Existing (not deleted) users by id.</summary>
    Task<IReadOnlyDictionary<int, UserSummary>> GetUsersAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    /// <summary>Active users, by name: the people work can be assigned to.</summary>
    Task<IReadOnlyList<UserSummary>> GetActiveUsersAsync(CancellationToken cancellationToken);

    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

/// <summary>Resource names for <c>IReferenceCheck</c>: other modules can veto deleting these.</summary>
public static class IdentityResources
{
    public const string User = "identity.user";
}
