using System.ComponentModel.DataAnnotations;

namespace CRM.Core.DTOs;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
