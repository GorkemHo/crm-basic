using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;

namespace CRM.Core.Mappings
{
    public static class UserMappings
    {
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public static void MapToEntity(
        this UpdateUserDto dto,
        User user)
        {
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Email = dto.Email;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
        }

        public static void MapPatchToEntity(
    this UpdateUserPatchDto dto,
    User user)
        {
            if(dto.FirstName != null)
                user.FirstName = dto.FirstName;

            if(dto.LastName != null)
                user.LastName = dto.LastName;

            if(dto.Email != null)
                user.Email = dto.Email;

            if(dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}
