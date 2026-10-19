using CRM.BuildingBlocks.Modules;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Infrastructure.Security;

/// <summary>Effective permissions of a set of roles. Admin always has every permission in the catalog.</summary>
internal sealed class PermissionResolver
{
    private readonly IdentityDbContext _db;
    private readonly ModuleCatalog _catalog;

    public PermissionResolver(IdentityDbContext db, ModuleCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public IReadOnlyList<string> AllPermissions => _catalog.Permissions.Select(x => x.Name).Order().ToList();

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(IReadOnlyCollection<Role> roles, CancellationToken cancellationToken)
    {
        var activeRoles = roles.Where(r => r.IsActive).ToList();

        if(activeRoles.Any(r => r.Name == SystemRoles.Admin))
        {
            return AllPermissions;
        }

        var roleIds = activeRoles.Select(r => r.Id).ToList();

        return await _db.RolePermissions
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.PermissionName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
}
