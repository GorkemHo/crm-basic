using CRM.BuildingBlocks.Modules;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Tasks.Infrastructure;

/// <summary>
/// Records with open tasks cannot be deleted: the work would be lost or end up with nobody to do it.
/// Done tasks don't block; they keep the id and show the name as missing.
/// </summary>
internal sealed class TasksReferenceCheck : IReferenceCheck
{
    private readonly TasksDbContext _db;

    public TasksReferenceCheck(TasksDbContext db) => _db = db;

    public async Task<ReferenceConflict?> CheckAsync(string resource, int id, CancellationToken cancellationToken)
    {
        var open = _db.Tasks.Where(x => x.CompletedAt == null);

        return resource switch
        {
            CrmResources.Company when await open.AnyAsync(x => x.CompanyId == id, cancellationToken) =>
                new ReferenceConflict("The company has open tasks and cannot be deleted.", "company.has_open_tasks"),
            CrmResources.Customer when await open.AnyAsync(x => x.CustomerId == id, cancellationToken) =>
                new ReferenceConflict("The customer has open tasks and cannot be deleted.", "customer.has_open_tasks"),
            SalesResources.Deal when await open.AnyAsync(x => x.DealId == id, cancellationToken) =>
                new ReferenceConflict("The deal has open tasks and cannot be deleted.", "deal.has_open_tasks"),
            IdentityResources.User when await open.AnyAsync(x => x.AssigneeId == id, cancellationToken) =>
                new ReferenceConflict("The user has open tasks. Reassign them before deleting the user.", "user.has_open_tasks"),
            _ => null
        };
    }
}
