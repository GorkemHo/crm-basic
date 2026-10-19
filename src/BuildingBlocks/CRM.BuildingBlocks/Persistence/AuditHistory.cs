using System.Text.Json;
using CRM.BuildingBlocks.Domain;
using CRM.BuildingBlocks.Pagination;
using Microsoft.EntityFrameworkCore;

namespace CRM.BuildingBlocks.Persistence;

/// <summary>A change of an entity as returned by <c>…/{id}/history</c> endpoints.</summary>
public sealed record AuditLogDto(long Id, string Action, JsonElement Changes, int? UserId, DateTime OccurredAt);

public static class AuditHistory
{
    /// <summary>Newest-first change history of one entity of type <typeparamref name="TEntity"/>.</summary>
    public static async Task<PagedResponse<AuditLogDto>> GetAsync<TEntity>(
        ModuleDbContext db,
        object entityId,
        PaginationRequest paging,
        CancellationToken cancellationToken)
    {
        var name = typeof(TEntity).Name;
        var id = entityId.ToString()!;

        var query = db.AuditLogs.AsNoTracking().Where(x => x.EntityName == name && x.EntityId == id);

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((paging.Page - 1) * paging.Size)
            .Take(paging.Size)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditLogDto>
        {
            Items = rows.Select(x => new AuditLogDto(
                x.Id, x.Action, JsonDocument.Parse(x.Changes).RootElement.Clone(), x.UserId, x.OccurredAt)).ToList(),
            PageNumber = paging.Page,
            PageSize = paging.Size,
            TotalCount = total
        };
    }
}
