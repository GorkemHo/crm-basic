using CRM.Core.Entities;

namespace CRM.Core.Interfaces
{
    public interface IUserRoleRepository
    {
        Task<IEnumerable<UserRole>> GetByUserIdAsync(int userId);

        Task<bool> AnyWithRoleAsync(int roleId);

        /// <summary>
        /// Replaces all roles of the user with the given role ids in a single transaction.
        /// </summary>
        Task ReplaceUserRolesAsync(int userId, IEnumerable<int> roleIds);
    }
}
