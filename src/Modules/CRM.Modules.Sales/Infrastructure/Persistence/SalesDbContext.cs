using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Sales.Infrastructure.Persistence;

internal sealed class SalesDbContext : ModuleDbContext
{
    public const string SchemaName = "sales";

    public SalesDbContext(DbContextOptions<SalesDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<PipelineStage> Stages => Set<PipelineStage>();
    public DbSet<Deal> Deals => Set<Deal>();
}

internal sealed class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> builder)
    {
        builder.ToTable("pipeline_stages");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique().HasFilter("is_deleted = false");
        builder.ToTable(t => t.HasCheckConstraint("ck_pipeline_stages_probability", "probability BETWEEN 0 AND 100"));
    }
}

internal sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
        builder.Property(x => x.LostReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.Status, x.StageId });
        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => x.CustomerId);

        // A stage that still has deals cannot be deleted.
        builder.HasOne(x => x.Stage)
            .WithMany()
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesDbContextFactory : ModuleDesignTimeDbContextFactory<SalesDbContext>
{
    protected override string Schema => SalesDbContext.SchemaName;
}
