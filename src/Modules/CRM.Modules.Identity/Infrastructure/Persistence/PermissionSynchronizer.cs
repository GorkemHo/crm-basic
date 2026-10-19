using CRM.BuildingBlocks.Modules;
using CRM.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Mirrors the permissions declared by the modules into <c>identity.permissions</c> (runs at every startup).
/// A permission seen for the first time is granted to the built-in User role when it is marked
/// <c>GrantedToUsersByDefault</c>; permissions no longer declared are removed from all roles.
/// </summary>
internal static class PermissionSynchronizer
{
    public static async Task SyncAsync(IdentityDbContext db, ModuleCatalog catalog, ILogger logger, CancellationToken cancellationToken)
    {
        var declared = catalog.Modules
            .SelectMany(m => m.Permissions.Select(p => (Module: m.Name, Definition: p)))
            .ToDictionary(x => x.Definition.Name);

        var existing = await db.Permissions.ToDictionaryAsync(x => x.Name, cancellationToken);
        var userRole = await db.Roles.FirstOrDefaultAsync(x => x.Name == SystemRoles.User, cancellationToken);

        foreach(var (name, (module, definition)) in declared)
        {
            if(existing.TryGetValue(name, out var permission))
            {
                permission.Module = module;
                permission.Group = definition.Group;
                permission.Description = definition.Description;
                continue;
            }

            db.Permissions.Add(new Permission
            {
                Name = name,
                Module = module,
                Group = definition.Group,
                Description = definition.Description
            });

            if(definition.GrantedToUsersByDefault && userRole is not null)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = userRole.Id, PermissionName = name });
            }

            logger.LogInformation("Registered permission {Permission}", name);
        }

        foreach(var stale in existing.Values.Where(p => !declared.ContainsKey(p.Name)))
        {
            db.Permissions.Remove(stale);
            logger.LogInformation("Removed permission {Permission}", stale.Name);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
