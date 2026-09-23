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
            .Include(x => x.Role)
            .Where(x => x.UserId == userId)
            .ToListAsync();
        }

        public async Task RemoveByUserIdAsync(
        int userId)
        {
            var roles = await _context.UserRoles
            .Where(x => x.UserId == userId)
            .ToListAsync();

            _context.UserRoles.RemoveRange(roles);

            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(
        IEnumerable<UserRole> userRoles)
        {
            await _context.UserRoles.AddRangeAsync(userRoles);

            await _context.SaveChangesAsync();
        }
    }
}
