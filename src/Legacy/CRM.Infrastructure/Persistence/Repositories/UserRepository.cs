using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(CrmDbContext context)
        : base(context)
        {
        }

        public override async Task<User?> GetByIdAsync(int id)
        {
            return await WithRoles(_dbSet)
            .FirstOrDefaultAsync(x => x.Id == id);
        }

        public override async Task<IEnumerable<User>> GetAllAsync()
        {
            return await WithRoles(_dbSet.AsNoTracking())
            .ToListAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await WithRoles(_dbSet)
            .FirstOrDefaultAsync(x => x.Email == email);
        }

        private static IQueryable<User> WithRoles(IQueryable<User> query)
        {
            return query
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role);
        }
    }
}
