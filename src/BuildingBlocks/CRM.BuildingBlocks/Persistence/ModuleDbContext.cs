using System.Linq.Expressions;
using System.Text.Json;
using CRM.BuildingBlocks.Domain;
using CRM.BuildingBlocks.Messaging;
using CRM.BuildingBlocks.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CRM.BuildingBlocks.Persistence;

/// <summary>
/// Base DbContext for a module. Each module owns one context and one PostgreSQL schema;
/// entity configurations are picked up from the module's assembly, soft-deleted rows are filtered out,
/// and an <c>audit_logs</c> table is included. Table and column names are snake_case by convention.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>PostgreSQL schema that holds every table of this module.</summary>
    public abstract string Schema { get; }

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Queues an integration event in this module's outbox. It is saved by the next <c>SaveChanges</c>,
    /// atomically with the business change, and delivered in the background afterwards.
    /// </summary>
    public void AddIntegrationEvent(IIntegrationEvent integrationEvent)
    {
        var type = integrationEvent.GetType();

        OutboxMessages.Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
            Content = JsonSerializer.Serialize(integrationEvent, type, JsonSerializerOptions.Web),
            OccurredAt = integrationEvent.OccurredAt
        });
    }

    /// <summary>
    /// Saves, then builds the event (so it can use database-generated ids) and saves it to the outbox,
    /// all in one transaction. Use for "created" events.
    /// </summary>
    public async Task SaveChangesAndPublishAsync(Func<IIntegrationEvent> createEvent, CancellationToken cancellationToken)
    {
        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        await SaveChangesAsync(cancellationToken);
        AddIntegrationEvent(createEvent());
        await SaveChangesAsync(cancellationToken);

        if(transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.Property(x => x.Type).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Content).HasColumnType("jsonb").IsRequired();
            // Pending messages are what the processor scans for.
            entity.HasIndex(x => x.OccurredAt).HasFilter("processed_at IS NULL");
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.EntityName, x.EntityId, x.OccurredAt });
        });

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach(var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            // e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted))),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            modelBuilder.Entity(entityType.ClrType).Property(nameof(ISoftDeletable.IsDeleted)).HasDefaultValue(false);
        }
    }
}
