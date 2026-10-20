using CRM.BuildingBlocks.Modules;
using CRM.Modules.Activities.Infrastructure.Persistence;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Activities.Infrastructure;

/// <summary>Companies, customers and deals with logged activities cannot be deleted (the history would be orphaned).</summary>
internal sealed class ActivitiesReferenceCheck : IReferenceCheck
{
    private readonly ActivitiesDbContext _db;

    public ActivitiesReferenceCheck(ActivitiesDbContext db) => _db = db;

    public async Task<ReferenceConflict?> CheckAsync(string resource, int id, CancellationToken cancellationToken) => resource switch
    {
        CrmResources.Company when await _db.Activities.AnyAsync(x => x.CompanyId == id, cancellationToken) =>
            new ReferenceConflict("The company has activities and cannot be deleted.", "company.has_activities"),
        CrmResources.Customer when await _db.Activities.AnyAsync(x => x.CustomerId == id, cancellationToken) =>
            new ReferenceConflict("The customer has activities and cannot be deleted.", "customer.has_activities"),
        SalesResources.Deal when await _db.Activities.AnyAsync(x => x.DealId == id, cancellationToken) =>
            new ReferenceConflict("The deal has activities and cannot be deleted.", "deal.has_activities"),
        _ => null
    };
}
