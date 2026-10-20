using CRM.Modules.Identity.Contracts;
using CRM.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Infrastructure;

internal sealed class UserDirectory : IUserDirectory
{
    private readonly IdentityDbContext _db;

    public UserDirectory(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<int, string>> GetNamesAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        if(wanted.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _db.Users.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => wanted.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, cancellationToken);
    }
}
