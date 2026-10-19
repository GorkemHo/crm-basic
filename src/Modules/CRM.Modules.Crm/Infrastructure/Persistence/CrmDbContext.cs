using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Crm.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Modules.Crm.Infrastructure.Persistence;

internal sealed class CrmDbContext : ModuleDbContext
{
    public const string SchemaName = "crm";

    public CrmDbContext(DbContextOptions<CrmDbContext> options)
        : base(options)
    {
    }

    public override string Schema => SchemaName;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Customer> Customers => Set<Customer>();
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.Property(x => x.CompanyName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TaxNumber).HasMaxLength(11);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.TaxNumber).IsUnique().HasFilter("tax_number IS NOT NULL AND is_deleted = false");
        builder.HasIndex(x => x.CompanyName);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Email).IsUnique().HasFilter("is_deleted = false");

        // A company that still has customers cannot be deleted.
        builder.HasOne(x => x.Company)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CrmDbContextFactory : ModuleDesignTimeDbContextFactory<CrmDbContext>
{
    protected override string Schema => CrmDbContext.SchemaName;
}
