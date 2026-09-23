using CRM.Core.DTOs.RoleDtos;
using CRM.Core.Entities;

namespace CRM.Core.Mappings
{
    public static class RoleMappings
    {
        public static RoleDto ToDto(this Role role)
        {
            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt
            };
        }
        public static Role ToEntity(this CreateRoleDto dto)
        {
            return new Role
            {
                Name = dto.Name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        public static void MapToEntity(this UpdateRoleDto dto, Role role)
        {
            role.Name = dto.Name;
            role.IsActive = dto.IsActive;
        }
    }
}
