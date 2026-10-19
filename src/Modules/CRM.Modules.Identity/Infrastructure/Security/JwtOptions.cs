using System.Text;

namespace CRM.Modules.Identity.Infrastructure.Security;

internal sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key; at least 32 bytes. Keep it in user-secrets / environment.</summary>
    public string SecretKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;

    public int RefreshTokenExpirationDays { get; set; } = 7;

    public bool HasValidKey => Encoding.UTF8.GetByteCount(SecretKey) >= 32;
}
