using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;

namespace CRM.Core.Services
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterUserDto request);
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
    }
}
