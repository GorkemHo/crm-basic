namespace CRM.Modules.Crm.Contracts;

public sealed record CompanySummary(int Id, string CompanyName, bool IsActive);

public sealed record CustomerSummary(int Id, int CompanyId, string FullName, string Email, bool IsActive);

/// <summary>Read-only lookups other modules use to reference companies and customers.</summary>
public interface ICrmDirectory
{
    Task<IReadOnlyDictionary<int, CompanySummary>> GetCompaniesAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, CustomerSummary>> GetCustomersAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    Task<CompanySummary?> FindCompanyByTaxNumberAsync(string taxNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSummary>> GetCustomersOfCompanyAsync(int companyId, CancellationToken cancellationToken);
}

/// <summary>Resource names for <c>IReferenceCheck</c>: other modules can veto deleting these.</summary>
public static class CrmResources
{
    public const string Company = "crm.company";
    public const string Customer = "crm.customer";
}
