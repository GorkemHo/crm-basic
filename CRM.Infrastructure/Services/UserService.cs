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
            var existingCustomer =
            await _userRepository.GetByEmailAsync(dto.Email);

            if(existingCustomer is not null)
            {
                throw new InvalidOperationException(
                "A user with the same email already exists.");
            }

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


            if(dto.FirstName != null)
                user.FirstName = dto.FirstName;


            if(dto.LastName != null)
                user.LastName = dto.LastName;

            if(dto.Email != null)
                user.Email = dto.Email;


            if(dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            user.UpdatedAt = DateTime.UtcNow;

            Console.WriteLine($"CreatedAt: {user.CreatedAt}");
            Console.WriteLine($"CreatedAt Kind: {user.CreatedAt.Kind}");

            Console.WriteLine($"UpdatedAt: {user.UpdatedAt}");
            Console.WriteLine($"UpdatedAt Kind: {user.UpdatedAt.Kind}");

            await _userRepository.UpdateAsync(user);


            return user;
        }
    }
}
