namespace CRM.Modules.Sales.Contracts;

public sealed record DealSummary(int Id, int CompanyId, string Title, string Status);

/// <summary>Read-only deal lookups other modules use to reference deals.</summary>
public interface ISalesDirectory
{
    Task<IReadOnlyDictionary<int, DealSummary>> GetDealsAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<DealSummary>> GetDealsOfCompanyAsync(int companyId, CancellationToken cancellationToken);
}

/// <summary>Resource names for <c>IReferenceCheck</c>: other modules can veto deleting these.</summary>
public static class SalesResources
{
    public const string Deal = "sales.deal";
}
