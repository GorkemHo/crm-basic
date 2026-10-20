using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Activities.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Activities.Infrastructure.Persistence;

internal sealed class ActivitiesDbContext : ModuleDbContext
{
    public const string SchemaName = "activities";

    public ActivitiesDbContext(DbContextOptions<ActivitiesDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<Activity> Activities => Set<Activity>();
}

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4000);

        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt });
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.DealId);
        builder.HasIndex(x => x.OccurredAt);
    }
}

internal sealed class ActivitiesDbContextFactory : ModuleDesignTimeDbContextFactory<ActivitiesDbContext>
{
    protected override string Schema => ActivitiesDbContext.SchemaName;
}
