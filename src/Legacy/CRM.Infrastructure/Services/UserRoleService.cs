using CRM.Core.DTOs.RoleDtos;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Exceptions;
using CRM.Core.Interfaces;
using CRM.Core.Services;

namespace CRM.Infrastructure.Services
{
    public class UserRoleService : IUserRoleService
    {
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;

        public UserRoleService(
        IUserRoleRepository userRoleRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository)
        {
            _userRoleRepository = userRoleRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        public async Task AssignRolesAsync(int userId, AssignRolesDto dto)
        {
            await EnsureUserExistsAsync(userId);

            var roleIds = dto.RoleIds.Distinct().ToList();

            var existingRoles = await _roleRepository
            .FindAsync(x => roleIds.Contains(x.Id));

            var missingRoleIds = roleIds
            .Except(existingRoles.Select(x => x.Id))
            .ToList();

            if(missingRoleIds.Count > 0)
            {
                throw new NotFoundException($"Role(s) not found: {string.Join(", ", missingRoleIds)}.", "role.not_found");
            }

            await _userRoleRepository
            .ReplaceUserRolesAsync(userId, roleIds);
        }

        public async Task<IEnumerable<UserRoleDto>> GetUserRolesAsync(int userId)
        {
            await EnsureUserExistsAsync(userId);

            var userRoles =
            await _userRoleRepository
            .GetByUserIdAsync(userId);

            return userRoles.Select(x => new UserRoleDto
            {
                RoleId = x.RoleId,
                RoleName = x.Role.Name
            });
        }

        private async Task EnsureUserExistsAsync(int userId)
        {
            if(!await _userRepository.ExistsAsync(x => x.Id == userId))
            {
                throw new NotFoundException("User", userId);
            }
        }
    }
}
