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

    public async Task<IReadOnlyDictionary<int, UserSummary>> GetUsersAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        return await _db.Users.AsNoTracking()
            .Where(x => wanted.Contains(x.Id))
            .Select(x => new UserSummary(x.Id, x.FirstName + " " + x.LastName, x.Email, x.IsActive))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummary>> GetActiveUsersAsync(CancellationToken cancellationToken) =>
        await _db.Users.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new UserSummary(x.Id, x.FirstName + " " + x.LastName, x.Email, x.IsActive))
            .ToListAsync(cancellationToken);

    public Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking()
            .Where(x => x.Email == email)
            .Select(x => new UserSummary(x.Id, x.FirstName + " " + x.LastName, x.Email, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
