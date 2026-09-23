using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;

namespace CRM.Core.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();

        Task<User?> GetByIdAsync(int id);

        Task<User?> GetByEmailAsync(string email);

        Task<User> CreateAsync(CreateUserDto user);

        Task<User?> UpdateAsync(int id, UpdateUserDto dto);

        Task<User?> PatchAsync(int id, UpdateUserPatchDto dto);

        Task<bool> DeleteAsync(int id);
    }
}
