using CRM.Core.Common;
using CRM.Core.Entities;
using CRM.Core.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CRM.Infrastructure.Authentication
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtSettings _jwtSettings;

        public JwtTokenGenerator(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        public string GenerateToken(
        User user,
        IEnumerable<string> roles)
        {
            var claims = new List<Claim>
                                {
                                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                                new(JwtRegisteredClaimNames.Email, user.Email),
                                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                                new(ClaimTypes.Email, user.Email),
                                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
                                };

            claims.AddRange(
            roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

            var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
            _jwtSettings.ExpirationMinutes),
            signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
            .WriteToken(token);
        }
    }
}
