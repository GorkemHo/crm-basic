using System.Security.Cryptography;
using System.Text;
using CRM.Core.Common;
using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Exceptions;
using CRM.Core.Interfaces;
using CRM.Core.Mappings;
using CRM.Core.Security;
using CRM.Core.Services;
using Microsoft.Extensions.Options;

namespace CRM.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private const string DefaultRoleName = Roles.User;

        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOptions<JwtSettings> jwtSettings)
        {
            _userRepository = userRepository;
            _jwtTokenService = jwtTokenService;
            _roleRepository = roleRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepository
            .GetByEmailAsync(request.Email);

            if(user is null)
            {
                throw new UnauthorizedException("Invalid email or password.", "auth.invalid_credentials");
            }

            var validPassword =
            BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

            if(!validPassword)
            {
                throw new UnauthorizedException("Invalid email or password.", "auth.invalid_credentials");
            }

            if(!user.IsActive)
            {
                throw new UnauthorizedException("User account is disabled.", "auth.user_disabled");
            }

            return await IssueTokensAsync(user);
        }

        public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
        {
            var storedToken = await _refreshTokenRepository
            .GetByTokenHashAsync(HashToken(refreshToken));

            if(storedToken is null)
            {
                throw new UnauthorizedException("Invalid refresh token.", "auth.invalid_refresh_token");
            }

            if(storedToken.IsRevoked)
            {
                // A revoked token being presented again means it may have been stolen:
                // invalidate every session of that user.
                await _refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserId);

                throw new UnauthorizedException("Invalid refresh token.", "auth.invalid_refresh_token");
            }

            if(storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new UnauthorizedException("Refresh token has expired.", "auth.refresh_token_expired");
            }

            if(!storedToken.User.IsActive)
            {
                throw new UnauthorizedException("User account is disabled.", "auth.user_disabled");
            }

            // Rotate: the presented token is single-use.
            storedToken.RevokedAt = DateTime.UtcNow;

            return await IssueTokensAsync(storedToken.User);
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var storedToken = await _refreshTokenRepository
            .GetByTokenHashAsync(HashToken(refreshToken));

            if(storedToken is null || storedToken.IsRevoked)
            {
                return;
            }

            storedToken.RevokedAt = DateTime.UtcNow;

            await _refreshTokenRepository.SaveChangesAsync();
        }

        public async Task RegisterAsync(RegisterUserDto request)
        {
            var existingUser =
            await _userRepository.GetByEmailAsync(request.Email);

            if(existingUser is not null)
            {
                throw new ConflictException("A user with this email already exists.", "user.email_taken");
            }

            // Self-registration always gets the default role; elevated roles are assigned by an Admin.
            var role =
            await _roleRepository.GetByNameAsync(DefaultRoleName);

            if(role is null)
            {
                // Missing seed data is a server misconfiguration, not a client error.
                throw new InvalidOperationException($"Default role '{DefaultRoleName}' does not exist.");
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

            await _userRepository.AddAsync(user);
        }

        private async Task<LoginResponseDto> IssueTokensAsync(User user)
        {
            var roles = user.UserRoles
            .Select(x => x.Role.Name)
            .ToList();

            var response = _jwtTokenService
            .GenerateTokens(user, roles);

            var now = DateTime.UtcNow;

            response.RefreshTokenExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpirationDays);
            response.Email = user.Email;
            response.User = user.ToDto();

            // Only a hash is stored, so a database leak does not expose usable tokens.
            await _refreshTokenRepository.AddAsync(new RefreshToken
            {
                UserId = user.Id,
                Token = HashToken(response.RefreshToken),
                CreatedAt = now,
                ExpiresAt = response.RefreshTokenExpiresAt
            });

            await _refreshTokenRepository.SaveChangesAsync();

            return response;
        }

        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
