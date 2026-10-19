using System.Linq.Expressions;
using CRM.BuildingBlocks.Domain;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

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
