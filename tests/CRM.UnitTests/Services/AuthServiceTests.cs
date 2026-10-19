using CRM.Core.Common;
using CRM.Core.DTOs;
using CRM.Core.DTOs.UserDtos;
using CRM.Core.Entities;
using CRM.Core.Exceptions;
using CRM.Core.Interfaces;
using CRM.Infrastructure.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CRM.Tests.Services;

public class AuthServiceTests
{
    private const string Password = "secret123";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var settings = Options.Create(new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            SecretKey = new string('k', 48),
            ExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        _sut = new AuthService(_users, new JwtTokenService(settings), _roles, _refreshTokens, settings);
    }

    private static User CreateUser(bool isActive = true, params string[] roles)
    {
        var user = new User
        {
            Id = 42,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            IsActive = isActive
        };

        foreach(var (name, index) in roles.Select((r, i) => (r, i)))
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = index + 1, Role = new Role { Id = index + 1, Name = name } });
        }

        return user;
    }

    [Fact]
    public async Task Register_AlwaysAssignsDefaultUserRole()
    {
        _roles.GetByNameAsync("User").Returns(new Role { Id = 2, Name = "User" });

        await _sut.RegisterAsync(new RegisterUserDto
        {
            FirstName = "Mallory",
            LastName = "M",
            Email = "mallory@example.com",
            Password = Password
        });

        await _roles.DidNotReceive().GetByNameAsync("Admin");
        await _users.Received(1).AddAsync(Arg.Is<User>(u =>
            u.UserRoles.Count == 1 && u.UserRoles.Single().RoleId == 2));
    }

    [Fact]
    public async Task Register_ExistingEmail_Throws()
    {
        _users.GetByEmailAsync("ada@example.com").Returns(CreateUser());

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(new RegisterUserDto
        {
            FirstName = "Ada",
            LastName = "L",
            Email = "ada@example.com",
            Password = Password
        }));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        _users.GetByEmailAsync("ada@example.com").Returns(CreateUser());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "ada@example.com", Password = "wrong" }));
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsUnauthorized()
    {
        _users.GetByEmailAsync("ada@example.com").Returns(CreateUser(isActive: false));

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "ada@example.com", Password = Password }));

        Assert.Contains("disabled", exception.Message);
    }

    [Fact]
    public async Task Login_Success_ReturnsRolesAndStoresHashedRefreshToken()
    {
        _users.GetByEmailAsync("ada@example.com").Returns(CreateUser(true, "Admin", "User"));

        var response = await _sut.LoginAsync(new LoginRequestDto { Email = "ada@example.com", Password = Password });

        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal(["Admin", "User"], response.Roles);
        Assert.Equal("Admin, User", response.User.RoleName);
        await _refreshTokens.Received(1).AddAsync(Arg.Is<RefreshToken>(t =>
            t.UserId == 42 && t.Token != response.RefreshToken && t.Token.Length == 64));
        await _refreshTokens.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Login_AccessToken_UsesShortClaimNames()
    {
        _users.GetByEmailAsync("ada@example.com").Returns(CreateUser(true, "Admin", "User"));

        var response = await _sut.LoginAsync(new LoginRequestDto { Email = "ada@example.com", Password = Password });
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Equal("42", token.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("ada@example.com", token.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Ada Lovelace", token.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal(["Admin", "User"], token.Claims.Where(c => c.Type == "role").Select(c => c.Value));
        Assert.DoesNotContain(token.Claims, c => c.Type.StartsWith("http"));
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesToken()
    {
        var stored = new RefreshToken { UserId = 42, User = CreateUser(true, "User"), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>()).Returns(stored);

        var response = await _sut.RefreshAsync("presented-token");

        Assert.NotNull(stored.RevokedAt);
        Assert.NotEqual("presented-token", response.RefreshToken);
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Refresh_RevokedToken_RevokesAllSessionsOfUser()
    {
        var stored = new RefreshToken
        {
            UserId = 42,
            User = CreateUser(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>()).Returns(stored);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.RefreshAsync("stolen-token"));

        await _refreshTokens.Received(1).RevokeAllForUserAsync(42);
        await _refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ThrowsUnauthorized()
    {
        var stored = new RefreshToken { UserId = 42, User = CreateUser(), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>()).Returns(stored);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.RefreshAsync("old-token"));
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsUnauthorized()
    {
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.RefreshAsync("nope"));
    }
}
