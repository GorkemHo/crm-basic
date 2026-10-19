using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Persistence.Repositories
{
    public class RoleRepository : Repository<Role>, IRoleRepository
    {
        public RoleRepository(CrmDbContext context) : base(context)
        {
        }

        public async Task<Role?> GetByNameAsync(string roleName)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == roleName);
        }
    }
}
