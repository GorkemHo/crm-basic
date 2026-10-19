using CRM.Core.DTOs.RoleDtos;
using CRM.Core.DTOs.UserDtos;

namespace CRM.Core.Services
{
    public interface IUserRoleService
    {
        Task AssignRolesAsync(int userId, AssignRolesDto dto);

        Task<IEnumerable<UserRoleDto>> GetUserRolesAsync(int userId);
    }
}
