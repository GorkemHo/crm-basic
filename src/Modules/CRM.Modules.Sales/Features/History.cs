using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Infrastructure.Persistence;

namespace CRM.Modules.Sales.Features;

internal sealed class GetDealHistory : PaginationRequest, IQuery<PagedResponse<AuditLogDto>>
{
    public int Id { get; set; }
}

internal sealed class GetDealHistoryHandler : IRequestHandler<GetDealHistory, PagedResponse<AuditLogDto>>
{
    private readonly SalesDbContext _db;

    public GetDealHistoryHandler(SalesDbContext db) => _db = db;

    public Task<PagedResponse<AuditLogDto>> Handle(GetDealHistory request, CancellationToken cancellationToken) =>
        AuditHistory.GetAsync<Deal>(_db, request.Id, request, cancellationToken);
}
