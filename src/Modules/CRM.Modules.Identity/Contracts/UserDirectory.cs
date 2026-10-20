namespace CRM.Modules.Identity.Contracts;

/// <summary>Read-only user lookups for other modules (e.g. to show who wrote a record).</summary>
public interface IUserDirectory
{
    /// <summary>Full names by user id; deleted users are included so old records still show their author.</summary>
    Task<IReadOnlyDictionary<int, string>> GetNamesAsync(IEnumerable<int> ids, CancellationToken cancellationToken);
}
