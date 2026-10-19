using CRM.Core.DTOs.RoleDtos;
using CRM.Core.Entities;

namespace CRM.Core.Services
{
    public interface IRoleService
    {
        Task<IEnumerable<Role>> GetAllAsync();

        Task<Role?> GetByIdAsync(int id);

        Task<Role?> GetByNameAsync(string name);

        Task<Role> CreateAsync(Role role);

        Task<Role?> UpdateAsync(int id, UpdateRoleDto dto);

        Task<bool> DeleteAsync(int id);

    }
}
