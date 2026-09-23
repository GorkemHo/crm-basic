using CRM.Core.DTOs.RoleDtos;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Core.Services;

namespace CRM.Infrastructure.Services
{
    public class UserRoleService : IUserRoleService
    {
        private readonly IUserRoleRepository _userRoleRepository;

        public UserRoleService(
        IUserRoleRepository userRoleRepository)
        {
            _userRoleRepository = userRoleRepository;
        }

        public async Task AssignRolesAsync(int userId, AssignRolesDto dto)
        {
            await _userRoleRepository
            .RemoveByUserIdAsync(userId);

            var userRoles = dto.RoleIds
            .Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });

            await _userRoleRepository
            .AddRangeAsync(userRoles);
        }

        public async Task<IEnumerable<UserRoleDto>> GetUserRolesAsync(int userId)
        {
            var userRoles =
            await _userRoleRepository
            .GetByUserIdAsync(userId);

            return userRoles.Select(x => new UserRoleDto
            {
                RoleId = x.RoleId,
                RoleName = x.Role.Name
            });
        }
    }
}
