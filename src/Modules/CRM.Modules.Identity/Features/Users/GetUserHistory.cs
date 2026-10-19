using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;

namespace CRM.Modules.Identity.Features.Users;

internal sealed class GetUserHistory : PaginationRequest, IQuery<PagedResponse<AuditLogDto>>
{
    public int Id { get; set; }
}

internal sealed class GetUserHistoryHandler : IRequestHandler<GetUserHistory, PagedResponse<AuditLogDto>>
{
    private readonly IdentityDbContext _db;

    public GetUserHistoryHandler(IdentityDbContext db) => _db = db;

    public Task<PagedResponse<AuditLogDto>> Handle(GetUserHistory request, CancellationToken cancellationToken) =>
        AuditHistory.GetAsync<User>(_db, request.Id, request, cancellationToken);
}
