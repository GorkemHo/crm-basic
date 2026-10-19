using System.Reflection;
using System.Text.Json;
using CRM.BuildingBlocks.Domain;
using CRM.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRM.BuildingBlocks.Persistence;

/// <summary>
/// Runs on every save of a module DbContext:
/// <list type="number">
/// <item>turns deletes of <see cref="ISoftDeletable"/> entities into updates that set <c>is_deleted</c>;</item>
/// <item>fills <see cref="IAuditable"/> timestamps and user ids;</item>
/// <item>writes an <see cref="AuditLogEntry"/> per updated/deleted auditable entity, in the same transaction.</item>
/// </list>
/// Creations are not logged: <c>created_at</c> / <c>created_by</c> already record them.
/// </summary>
internal sealed class AuditingInterceptor : SaveChangesInterceptor
{
    // Bookkeeping columns are not interesting as "changes".
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy),
        nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
        nameof(ISoftDeletable.IsDeleted), nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletedBy)
    ];

    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AuditingInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if(eventData.Context is ModuleDbContext context)
        {
            Apply(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if(eventData.Context is ModuleDbContext context)
        {
            Apply(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Apply(ModuleDbContext context)
    {
        context.ChangeTracker.DetectChanges();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userId = _currentUser.Id;
        var auditEntries = new List<AuditLogEntry>();

        foreach(var entry in context.ChangeTracker.Entries().ToList())
        {
            if(entry.Entity is AuditLogEntry || entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var deleted = entry.State == EntityState.Deleted;

            if(deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = now;
                softDeletable.DeletedBy = userId;
            }

            if(entry.Entity is not IAuditable auditable)
            {
                continue;
            }

            switch(entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedBy ??= userId;
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                    break;
            }

            if(entry.State != EntityState.Added)
            {
                var changes = deleted ? Snapshot(entry) : Diff(entry);

                if(deleted || changes.Count > 0)
                {
                    auditEntries.Add(new AuditLogEntry
                    {
                        EntityName = entry.Metadata.ClrType.Name,
                        EntityId = string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties
                            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())),
                        Action = deleted ? AuditAction.Deleted : AuditAction.Updated,
                        Changes = JsonSerializer.Serialize(changes, JsonSerializerOptions.Web),
                        UserId = userId,
                        OccurredAt = now
                    });
                }
            }
        }

        context.Set<AuditLogEntry>().AddRange(auditEntries);
    }

    private static Dictionary<string, object?> Diff(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach(var property in AuditedProperties(entry).Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
        {
            changes[ToCamelCase(property.Metadata.Name)] = new { old = property.OriginalValue, @new = property.CurrentValue };
        }

        return changes;
    }

    /// <summary>For deletes: the last values, so the record can be understood (or restored) later.</summary>
    private static Dictionary<string, object?> Snapshot(EntityEntry entry) =>
        AuditedProperties(entry).ToDictionary(p => ToCamelCase(p.Metadata.Name), p => (object?)new { old = p.OriginalValue });

    private static IEnumerable<PropertyEntry> AuditedProperties(EntityEntry entry) =>
        entry.Properties.Where(p =>
            !p.Metadata.IsPrimaryKey()
            && !IgnoredProperties.Contains(p.Metadata.Name)
            && p.Metadata.PropertyInfo?.GetCustomAttribute<NotAuditedAttribute>() is null);

    private static string ToCamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
