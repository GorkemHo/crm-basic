using CRM.Core.Entities;
using CRM.Infrastructure.Persistence.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");

            entity.HasKey(e => e.Id)
            .HasName("companies_pkey");

            entity.HasIndex(e => e.TaxNumber)
            .IsUnique();

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

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnName("id");

            entity.Property(e => e.FirstName)
            .HasColumnName("first_name");

            entity.Property(e => e.LastName)
            .HasColumnName("last_name");

            entity.Property(e => e.Email)
            .HasColumnName("email");

            entity.Property(e => e.PhoneNumber)
            .HasColumnName("phone_number");

            entity.Property(e => e.IsActive)
            .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

            entity.Property(e => e.CompanyId)
            .HasColumnName("company_id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
            .HasColumnName("id");

            entity.Property(x => x.FirstName)
            .HasColumnName("first_name");

            entity.Property(x => x.LastName)
            .HasColumnName("last_name");

            entity.Property(x => x.Email)
            .HasColumnName("email");

            entity.Property(x => x.PasswordHash)
            .HasColumnName("password_hash");

            entity.Property(x => x.IsActive)
            .HasColumnName("is_active");

            entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

            entity.HasIndex(x => x.Email)
            .IsUnique();
        });
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
            .HasColumnName("id");

            entity.Property(x => x.Name)
            .HasColumnName("name");

            entity.Property(x => x.IsActive)
            .HasColumnName("is_active");

            entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

            entity.HasIndex(x => x.Name)
            .IsUnique();
        });
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");

            entity.HasKey(x => new
            {
                x.UserId,
                x.RoleId
            });

            entity.Property(x => x.UserId)
            .HasColumnName("user_id");

            entity.Property(x => x.RoleId)
            .HasColumnName("role_id");

            entity.HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId);

            entity.HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId);
        });
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.Token)
                .HasColumnName("token");

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.RevokedAt)
                .HasColumnName("revoked_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
