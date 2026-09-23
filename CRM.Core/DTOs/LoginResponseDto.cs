using CRM.Core.DTOs.UserDtos;

namespace CRM.Core.DTOs;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public IEnumerable<string> Roles { get; set; }
        = Enumerable.Empty<string>();

    public UserDto User { get; set; }
}