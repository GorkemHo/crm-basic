using CRM.Modules.Sales.Contracts;
using CRM.Modules.Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Infrastructure;

internal sealed class SalesDirectory : ISalesDirectory
{
    private readonly SalesDbContext _db;

    public SalesDirectory(SalesDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<int, DealSummary>> GetDealsAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        return await _db.Deals.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .Select(x => new DealSummary(x.Id, x.CompanyId, x.Title, x.Status))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<DealSummary>> GetDealsOfCompanyAsync(int companyId, CancellationToken cancellationToken) =>
        await _db.Deals.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Id)
            .Select(x => new DealSummary(x.Id, x.CompanyId, x.Title, x.Status))
            .ToListAsync(cancellationToken);
}
