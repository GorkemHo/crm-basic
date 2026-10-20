using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationsDbContext : ModuleDbContext
{
    public const string SchemaName = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<Notification> Notifications => Set<Notification>();
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Link).HasMaxLength(200);

        // "My latest" and "my unread count".
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.UserId).HasFilter("read_at IS NULL").HasDatabaseName("ix_notifications_user_id_unread");
        // An event delivered twice creates its notifications once.
        builder.HasIndex(x => new { x.SourceEventId, x.UserId }).IsUnique();
    }
}

internal sealed class NotificationsDbContextFactory : ModuleDesignTimeDbContextFactory<NotificationsDbContext>
{
    protected override string Schema => NotificationsDbContext.SchemaName;
}
