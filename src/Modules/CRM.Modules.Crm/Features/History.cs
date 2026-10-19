using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Crm.Domain;
using CRM.Modules.Crm.Infrastructure.Persistence;

namespace CRM.Modules.Crm.Features;

internal sealed class GetCompanyHistory : PaginationRequest, IQuery<PagedResponse<AuditLogDto>>
{
    public int Id { get; set; }
}

internal sealed class GetCustomerHistory : PaginationRequest, IQuery<PagedResponse<AuditLogDto>>
{
    public int Id { get; set; }
}

internal sealed class HistoryHandlers :
    IRequestHandler<GetCompanyHistory, PagedResponse<AuditLogDto>>,
    IRequestHandler<GetCustomerHistory, PagedResponse<AuditLogDto>>
{
    private readonly CrmDbContext _db;

    public HistoryHandlers(CrmDbContext db) => _db = db;

    public Task<PagedResponse<AuditLogDto>> Handle(GetCompanyHistory request, CancellationToken cancellationToken) =>
        AuditHistory.GetAsync<Company>(_db, request.Id, request, cancellationToken);

    public Task<PagedResponse<AuditLogDto>> Handle(GetCustomerHistory request, CancellationToken cancellationToken) =>
        AuditHistory.GetAsync<Customer>(_db, request.Id, request, cancellationToken);
}
