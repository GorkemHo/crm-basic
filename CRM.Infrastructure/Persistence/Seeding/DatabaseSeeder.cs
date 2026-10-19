using CRM.Core.Entities;
using CRM.Core.Security;
using CRM.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Infrastructure.Persistence.Seeding;

public static class DatabaseSeeder
{
    /// <summary>
    /// Development helper: applies pending migrations, creates the built-in roles and, when
    /// <c>Seed:AdminEmail</c> and <c>Seed:AdminPassword</c> are configured, an Admin user. Idempotent.
    /// </summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSeeder));

        await db.Database.MigrateAsync(cancellationToken);

        var existingRoles = await db.Roles.Select(x => x.Name).ToListAsync(cancellationToken);

        foreach(var roleName in Roles.All.Except(existingRoles))
        {
            db.Roles.Add(new Role { Name = roleName, IsActive = true, CreatedAt = DateTime.UtcNow });
            logger.LogInformation("Seeded role {Role}", roleName);
        }

        await db.SaveChangesAsync(cancellationToken);

        var adminEmail = configuration["Seed:AdminEmail"];
        var adminPassword = configuration["Seed:AdminPassword"];

        if(string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)
           || await db.Users.AnyAsync(x => x.Email == adminEmail, cancellationToken))
        {
            return;
        }

        var roleIds = await db.Roles
            .Where(x => x.Name == Roles.Admin || x.Name == Roles.User)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var admin = new User
        {
            FirstName = "System",
            LastName = "Admin",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach(var roleId in roleIds)
        {
            admin.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded admin user {Email}", adminEmail);
    }
}
