using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Services;

namespace CRM.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _jwtTokenService = jwtTokenService;
            _roleRepository = roleRepository;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepository
            .GetByEmailAsync(request.Email);

            if(user is null)
            {
                throw new UnauthorizedAccessException(
                "Invalid credentials.");
            }

            var validPassword =
            BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

            if(!validPassword)
            {
                throw new UnauthorizedAccessException(
                "Invalid credentials.");
            }

            var roles = user.UserRoles
            .Select(x => x.Role.Name);

            var returnToken = _jwtTokenService
            .GenerateTokens(user, roles);

            returnToken.User = user.ToDto();

            return returnToken;
        }

        public async Task RegisterAsync(RegisterUserDto request)
        {
            var existingUser =
            await _userRepository.GetByEmailAsync(request.Email);

            if(existingUser is not null)
            {
                throw new InvalidOperationException(
                "User already exists.");
            }

            var role =
            await _roleRepository.GetByNameAsync(request.RoleName);

            if(role is null)
            {
                throw new KeyNotFoundException(
                "Role not found.");
            }

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.UserRoles.Add(new UserRole
            {
                RoleId = role.Id
            });

            //User CRUD 
            await _userRepository.AddAsync(user);
        }
    }
}
