using CRM.BuildingBlocks.Modules;
using CRM.Modules.Attachments.Domain;
using CRM.Modules.Attachments.Infrastructure.Persistence;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Sales.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Attachments.Infrastructure;

/// <summary>Records with files cannot be deleted; the files would be unreachable.</summary>
internal sealed class AttachmentsReferenceCheck : IReferenceCheck
{
    private readonly AttachmentsDbContext _db;

    public AttachmentsReferenceCheck(AttachmentsDbContext db) => _db = db;

    public async Task<ReferenceConflict?> CheckAsync(string resource, int id, CancellationToken cancellationToken)
    {
        var (recordType, code) = resource switch
        {
            CrmResources.Company => (RecordTypes.Company, "company.has_attachments"),
            CrmResources.Customer => (RecordTypes.Customer, "customer.has_attachments"),
            SalesResources.Deal => (RecordTypes.Deal, "deal.has_attachments"),
            _ => (null, null)
        };

        return recordType is not null && await _db.Attachments.AnyAsync(x => x.RecordType == recordType && x.RecordId == id, cancellationToken)
            ? new ReferenceConflict($"The {recordType} has files and cannot be deleted. Delete the files first.", code!)
            : null;
    }
}
