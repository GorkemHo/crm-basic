using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Features.Roles;
using CRM.Modules.Identity.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Demo users and a "Sales Manager" role, created through the module's commands.
/// Password for every demo user: <c>Seed:DemoPassword</c> (default <see cref="DefaultPassword"/>).
/// </summary>
internal static class IdentityDemoData
{
    public const string DefaultPassword = "Demo1234!";
    public const string SalesManagerRole = "Sales Manager";
    private const string CrmModuleName = "crm";

    /// <summary>Marker: when this user exists the demo data is already in place.</summary>
    public const string MarkerEmail = "ayse.yilmaz@demo.local";

    internal sealed record DemoUser(string FirstName, string LastName, string Email, string Language, bool Manager, bool Active = true);

    public static readonly IReadOnlyList<DemoUser> Users =
    [
        new("Ayşe", "Yılmaz", MarkerEmail, Languages.Turkish, Manager: true),
        new("Mehmet", "Kaya", "mehmet.kaya@demo.local", Languages.Turkish, Manager: false),
        new("Elif", "Demir", "elif.demir@demo.local", Languages.English, Manager: false),
        new("Can", "Öztürk", "can.ozturk@demo.local", Languages.Turkish, Manager: false, Active: false),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var sender = services.GetRequiredService<ISender>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentityDemoData));

        if(await db.Users.AnyAsync(x => x.Email == MarkerEmail, cancellationToken))
        {
            return;
        }

        var password = services.GetRequiredService<IConfiguration>()["Seed:DemoPassword"] ?? DefaultPassword;

        // A custom role that can do everything in the CRM module and see (not manage) users.
        // CRM permissions come from the module catalog, so Identity doesn't reference the Crm module.
        var crmPermissions = services.GetRequiredService<ModuleCatalog>().Modules
            .Where(m => m.Name == CrmModuleName)
            .SelectMany(m => m.Permissions)
            .Select(p => p.Name);

        var managerRole = await db.Roles.FirstOrDefaultAsync(x => x.Name == SalesManagerRole, cancellationToken)
            is { } existing
            ? existing.Id
            : (await sender.Send(new CreateRole(SalesManagerRole), cancellationToken)).Id;

        await sender.Send(
            new SetRolePermissions(managerRole, [.. crmPermissions, IdentityPermissions.UsersRead]),
            cancellationToken);

        var userRole = await db.Roles.Where(x => x.Name == SystemRoles.User).Select(x => x.Id).SingleAsync(cancellationToken);

        foreach(var demo in Users)
        {
            int[] roleIds = demo.Manager ? [userRole, managerRole] : [userRole];

            var user = await sender.Send(
                new CreateUser(demo.FirstName, demo.LastName, demo.Email, password, demo.Language, roleIds),
                cancellationToken);

            if(!demo.Active)
            {
                await sender.Send(new UpdateUser(user.Id, null, null, null, IsActive: false, null), cancellationToken);
            }
        }

        logger.LogInformation("Seeded {Count} demo users (password from Seed:DemoPassword)", Users.Count);
    }
}
