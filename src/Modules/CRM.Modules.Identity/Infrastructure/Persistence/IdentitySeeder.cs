using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Creates the built-in roles and, when <c>Seed:AdminEmail</c> / <c>Seed:AdminPassword</c> are configured,
/// an admin user (<c>Seed:AdminLanguage</c>, default <c>tr</c>). Idempotent.
/// </summary>
internal static class IdentitySeeder
{
    public static async Task SeedAsync(
        IdentityDbContext db,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existingRoles = await db.Roles.Select(x => x.Name).ToListAsync(cancellationToken);

        foreach(var name in SystemRoles.All.Except(existingRoles))
        {
            db.Roles.Add(new Role { Name = name });
            logger.LogInformation("Seeded role {Role}", name);
        }

        await db.SaveChangesAsync(cancellationToken);

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)
           || await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return;
        }

        var language = configuration["Seed:AdminLanguage"];

        var admin = new User
        {
            FirstName = "System",
            LastName = "Admin",
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            PreferredLanguage = language is not null && Languages.Supported.Contains(language) ? language : Languages.Default
        };

        foreach(var role in await db.Roles.Where(x => SystemRoles.All.Contains(x.Name)).ToListAsync(cancellationToken))
        {
            admin.UserRoles.Add(new UserRole { Role = role });
        }

        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded admin user {Email}", email);
    }
}
