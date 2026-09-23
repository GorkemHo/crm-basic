using CRM.Core.Entities;
using CRM.Infrastructure.Persistence.Repositories;

namespace CRM.Core.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
    }
}
