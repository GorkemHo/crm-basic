using CRM.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Context;

public partial class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");

            entity.HasKey(e => e.Id)
            .HasName("companies_pkey");

            entity.HasIndex(e => e.TaxNumber)
            .IsUnique()
            .HasDatabaseName("companies_tax_number_key");

            entity.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

            entity.Property(e => e.CompanyName)
            .HasColumnName("company_name");

            entity.Property(e => e.TaxNumber)
            .HasColumnName("tax_number");

            entity.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");

            entity.HasKey(e => e.Id)
            .HasName("customers_pkey");

            entity.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("customers_email_key");

            entity.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

            entity.Property(e => e.FirstName)
            .HasColumnName("first_name");

            entity.Property(e => e.LastName)
            .HasColumnName("last_name");

            entity.Property(e => e.Email)
            .HasColumnName("email");

            entity.Property(e => e.PhoneNumber)
            .HasColumnName("phone_number");

            entity.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.CompanyId)
            .HasColumnName("company_id");

            entity.HasIndex(e => e.CompanyId)
            .HasDatabaseName("ix_customers_company_id");

            // A company that still has customers cannot be deleted.
            entity.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("fk_customers_companies")
            .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(x => x.Id)
            .HasName("users_pkey");

            entity.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("users_email_key");

            entity.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

            entity.Property(x => x.FirstName)
            .HasColumnName("first_name");

            entity.Property(x => x.LastName)
            .HasColumnName("last_name");

            entity.Property(x => x.Email)
            .HasColumnName("email");

            entity.Property(x => x.PasswordHash)
            .HasColumnName("password_hash");

            entity.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasKey(x => x.Id)
            .HasName("roles_pkey");

            entity.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("roles_name_key");

            entity.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

            entity.Property(x => x.Name)
            .HasColumnName("name");

            entity.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");

            entity.HasKey(x => new
            {
                x.UserId,
                x.RoleId
            })
            .HasName("user_roles_pkey");

            entity.HasIndex(x => x.RoleId)
            .HasDatabaseName("ix_user_roles_role_id");

            entity.Property(x => x.UserId)
            .HasColumnName("user_id");

            entity.Property(x => x.RoleId)
            .HasColumnName("role_id");

            entity.HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("fk_user_roles_user")
            .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .HasConstraintName("fk_user_roles_role")
            .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(e => e.Id)
            .HasName("refresh_tokens_pkey");

            entity.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_refresh_tokens_user_id");

            entity.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseIdentityAlwaysColumn();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.Token)
                .HasColumnName("token");

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.RevokedAt)
                .HasColumnName("revoked_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("fk_refresh_tokens_users")
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
