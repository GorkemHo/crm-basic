using CRM.Core.DTOs.RoleDtos;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Services;

namespace CRM.Infrastructure.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<IEnumerable<Role>> GetAllAsync()
        {
            return await _roleRepository.GetAllAsync();
        }

        public async Task<Role?> GetByIdAsync(int id)
        {
            return await _roleRepository.GetByIdAsync(id);
        }

        public async Task<Role?> GetByNameAsync(string name)
        {
            return await _roleRepository.GetByNameAsync(name);
        }

        public async Task<Role> CreateAsync(Role role)
        {
            var existingRole =
            await _roleRepository.GetByNameAsync(role.Name);

            if (existingRole is not null)
            {
                throw new InvalidOperationException(
                "A role with the same name already exists.");
            }

            return await _roleRepository.AddAsync(role);
        }

        public async Task<Role?> UpdateAsync(int id, UpdateRoleDto dto)
        {
            var role = await _roleRepository.GetByIdAsync(id);

            if (role is null)
            {
                return null;
            }

            dto.MapToEntity(role);

            await _roleRepository.UpdateAsync(role);

            return role;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);

            if (role is null)
            {
                return false;
            }

            await _roleRepository.DeleteAsync(role);

            return true;
        }
    }
}
