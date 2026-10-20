using CRM.BuildingBlocks.Modules;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Infrastructure;

/// <summary>Companies and customers that have deals (open or closed) cannot be deleted.</summary>
internal sealed class SalesReferenceCheck : IReferenceCheck
{
    private readonly SalesDbContext _db;

    public SalesReferenceCheck(SalesDbContext db) => _db = db;

    public async Task<ReferenceConflict?> CheckAsync(string resource, int id, CancellationToken cancellationToken) => resource switch
    {
        CrmResources.Company when await _db.Deals.AnyAsync(x => x.CompanyId == id, cancellationToken) =>
            new ReferenceConflict("The company has deals and cannot be deleted.", "company.has_deals"),
        CrmResources.Customer when await _db.Deals.AnyAsync(x => x.CustomerId == id, cancellationToken) =>
            new ReferenceConflict("The customer is the contact of deals and cannot be deleted.", "customer.has_deals"),
        _ => null
    };
}
