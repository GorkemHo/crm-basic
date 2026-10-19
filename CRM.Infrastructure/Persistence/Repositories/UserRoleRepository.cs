using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class UserRoleRepository : IUserRoleRepository
    {
        private readonly CrmDbContext _context;

        public UserRoleRepository(CrmDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserRole>> GetByUserIdAsync(
        int userId)
        {
            return await _context.UserRoles
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.UserId == userId)
            .ToListAsync();
        }

        public async Task ReplaceUserRolesAsync(
        int userId,
        IEnumerable<int> roleIds)
        {
            var existing = await _context.UserRoles
            .Where(x => x.UserId == userId)
            .ToListAsync();

            _context.UserRoles.RemoveRange(existing);

            await _context.UserRoles.AddRangeAsync(
            roleIds.Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId
            }));

            // A single SaveChanges runs the deletes and inserts in one transaction.
            await _context.SaveChangesAsync();
        }
    }
}
