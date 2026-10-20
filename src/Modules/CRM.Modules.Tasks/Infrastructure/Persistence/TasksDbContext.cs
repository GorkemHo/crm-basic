using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Tasks.Infrastructure.Persistence;

internal sealed class TasksDbContext : ModuleDbContext
{
    public const string SchemaName = "tasks";

    public TasksDbContext(DbContextOptions<TasksDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
}

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.Ignore(x => x.IsDone);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Priority).HasMaxLength(10).IsRequired();

        // "My open tasks by due date" is the main query.
        builder.HasIndex(x => new { x.AssigneeId, x.CompletedAt, x.DueDate });
        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.DealId);
    }
}

internal sealed class TasksDbContextFactory : ModuleDesignTimeDbContextFactory<TasksDbContext>
{
    protected override string Schema => TasksDbContext.SchemaName;
}
