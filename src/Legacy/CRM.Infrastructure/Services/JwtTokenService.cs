using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CRM.Core.Common;
using CRM.Core.DTOs;
using CRM.Core.Entities;
using CRM.Core.Security;
using CRM.Core.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CRM.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateAccessToken(
        User user,
        IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(CrmClaimTypes.UserId, user.Id.ToString()),
            new(CrmClaimTypes.Email, user.Email),
            new(CrmClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(CrmClaimTypes.GivenName, user.FirstName),
            new(CrmClaimTypes.FamilyName, user.LastName),
            new(CrmClaimTypes.TokenId, Guid.NewGuid().ToString())
        };

        claims.AddRange(
            roles.Select(role => new Claim(CrmClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _jwtSettings.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];

        using var rng = RandomNumberGenerator.Create();

        rng.GetBytes(randomBytes);

        return Convert.ToBase64String(randomBytes);
    }

    public LoginResponseDto GenerateTokens(
        User user,
        IEnumerable<string> roles)
    {
        var accessToken = GenerateAccessToken(user, roles);

        var refreshToken = GenerateRefreshToken();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpirationMinutes),
            Roles = roles.ToArray()
        };
    }
}