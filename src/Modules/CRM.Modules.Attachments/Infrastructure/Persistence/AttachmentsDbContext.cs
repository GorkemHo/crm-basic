using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Attachments.Infrastructure.Persistence;

internal sealed class AttachmentsDbContext : ModuleDbContext
{
    public const string SchemaName = "attachments";

    public AttachmentsDbContext(DbContextOptions<AttachmentsDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<Attachment> Attachments => Set<Attachment>();
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.Property(x => x.RecordType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(32).IsRequired();

        builder.HasIndex(x => new { x.RecordType, x.RecordId });
    }
}

internal sealed class AttachmentsDbContextFactory : ModuleDesignTimeDbContextFactory<AttachmentsDbContext>
{
    protected override string Schema => AttachmentsDbContext.SchemaName;
}
