using CRM.Modules.Crm.Contracts;
using CRM.Modules.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Crm.Infrastructure;

internal sealed class CrmDirectory : ICrmDirectory
{
    private readonly CrmDbContext _db;

    public CrmDirectory(CrmDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<int, CompanySummary>> GetCompaniesAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        return await _db.Companies.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .Select(x => new CompanySummary(x.Id, x.CompanyName, x.IsActive))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, CustomerSummary>> GetCustomersAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        return await _db.Customers.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .Select(x => new CustomerSummary(x.Id, x.CompanyId, x.FirstName + " " + x.LastName, x.Email, x.IsActive))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    public Task<CompanySummary?> FindCompanyByTaxNumberAsync(string taxNumber, CancellationToken cancellationToken) =>
        _db.Companies.AsNoTracking()
            .Where(x => x.TaxNumber == taxNumber)
            .Select(x => new CompanySummary(x.Id, x.CompanyName, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersOfCompanyAsync(int companyId, CancellationToken cancellationToken) =>
        await _db.Customers.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Id)
            .Select(x => new CustomerSummary(x.Id, x.CompanyId, x.FirstName + " " + x.LastName, x.Email, x.IsActive))
            .ToListAsync(cancellationToken);
}
