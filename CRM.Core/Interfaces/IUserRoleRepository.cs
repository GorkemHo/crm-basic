using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface IUserRoleRepository
    {
        Task<IEnumerable<UserRole>> GetByUserIdAsync(int userId);

        Task RemoveByUserIdAsync(int userId);

        Task AddRangeAsync(IEnumerable<UserRole> userRoles);
    }
}
