using CRM.Core.Entities;
using CRM.Infrastructure.Persistence.Repositories;

namespace CRM.Core.Interfaces
{
    public interface IRoleRepository : IRepository<Role>
    {
        Task<Role?> GetByNameAsync(string roleName);
    }
}
