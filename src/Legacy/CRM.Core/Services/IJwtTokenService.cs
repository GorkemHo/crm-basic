using CRM.Core.DTOs;
using CRM.Core.Entities;

namespace CRM.Core.Services
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(
        User user,
        IEnumerable<string> roles);

        string GenerateRefreshToken();

        LoginResponseDto GenerateTokens(
        User user,
        IEnumerable<string> roles);
    }
}
