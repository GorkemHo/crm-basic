using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CRM.Modules.Identity.Infrastructure.Security;

internal sealed record IssuedTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

internal interface ITokenService
{
    /// <summary>Creates an access token and a new refresh token (not yet persisted).</summary>
    IssuedTokens Issue(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);

    string HashRefreshToken(string refreshToken);
}

internal sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public IssuedTokens Issue(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var accessExpiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(CrmClaimTypes.UserId, user.Id.ToString()),
            new(CrmClaimTypes.Email, user.Email),
            new(CrmClaimTypes.Name, user.FullName),
            new(CrmClaimTypes.GivenName, user.FirstName),
            new(CrmClaimTypes.FamilyName, user.LastName),
            new(CrmClaimTypes.Locale, user.PreferredLanguage),
            new(CrmClaimTypes.TokenId, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(CrmClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(CrmClaimTypes.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpiresAt,
            signingCredentials: credentials);

        return new IssuedTokens(
            new JwtSecurityTokenHandler().WriteToken(token),
            accessExpiresAt,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            now.AddDays(_options.RefreshTokenExpirationDays));
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

internal interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
