using System.IdentityModel.Tokens.Jwt;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CRM.UnitTests.Identity;

public class TokenServiceTests
{
    private readonly TokenService _sut = new(
        Options.Create(new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            SecretKey = new string('k', 48),
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        }),
        TimeProvider.System);

    private static readonly User Ada = new()
    {
        Id = 42,
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        PreferredLanguage = "en"
    };

    [Fact]
    public void Issue_AccessToken_UsesShortClaimNames()
    {
        var tokens = _sut.Issue(Ada, ["Admin", "User"], ["crm.companies.read"]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);

        Assert.Equal("42", jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("ada@example.com", jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Ada Lovelace", jwt.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal("en", jwt.Claims.Single(c => c.Type == "locale").Value);
        Assert.Equal(["Admin", "User"], jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value));
        Assert.Equal(["crm.companies.read"], jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value));
        Assert.DoesNotContain(jwt.Claims, c => c.Type.StartsWith("http"));
    }

    [Fact]
    public void Issue_SetsExpiries()
    {
        var tokens = _sut.Issue(Ada, [], []);

        Assert.InRange(tokens.AccessTokenExpiresAt, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        Assert.InRange(tokens.RefreshTokenExpiresAt, DateTime.UtcNow.AddDays(6.9), DateTime.UtcNow.AddDays(7.1));
    }

    [Fact]
    public void Issue_RefreshTokensAreUnique_AndHashIsStableHex()
    {
        var first = _sut.Issue(Ada, [], []).RefreshToken;
        var second = _sut.Issue(Ada, [], []).RefreshToken;

        Assert.NotEqual(first, second);
        Assert.Equal(_sut.HashRefreshToken(first), _sut.HashRefreshToken(first));
        Assert.Matches("^[0-9A-F]{64}$", _sut.HashRefreshToken(first));
    }
}
