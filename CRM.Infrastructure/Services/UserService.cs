using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Services;

namespace CRM.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _userRepository.GetAllAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task<User> CreateAsync(CreateUserDto dto)
        {
            await EnsureEmailIsUniqueAsync(dto.Email, excludeId: null);

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return await _userRepository.AddAsync(user);
        }

        public async Task<User?> UpdateAsync(
        int id,
        UpdateUserDto dto)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if(user is null)
            {
                return null;
            }

            await EnsureEmailIsUniqueAsync(dto.Email, excludeId: id);

            dto.MapToEntity(user);

            await _userRepository.UpdateAsync(user);

            return user;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if(user is null)
            {
                return false;
            }

            await _userRepository.DeleteAsync(user);

            return true;
        }

        public async Task<User?> PatchAsync(int id, UpdateUserPatchDto dto)
        {
            var user = await _userRepository
                .GetByIdAsync(id);

            if(user == null)
                return null;

            if(dto.Email != null)
            {
                await EnsureEmailIsUniqueAsync(dto.Email, excludeId: id);
            }

            dto.MapPatchToEntity(user);

            await _userRepository.UpdateAsync(user);

            return user;
        }

        private async Task EnsureEmailIsUniqueAsync(string email, int? excludeId)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email);

            if(existingUser is not null && existingUser.Id != excludeId)
            {
                throw new InvalidOperationException(
                "A user with the same email already exists.");
            }
        }
    }
}
