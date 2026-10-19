using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;

namespace CRM.Modules.Identity.Features.Auth;

/// <summary>Issues a token pair for a user and stores the refresh token's hash.</summary>
internal sealed class AuthSessionFactory
{
    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly PermissionResolver _permissions;
    private readonly TimeProvider _timeProvider;

    public AuthSessionFactory(IdentityDbContext db, ITokenService tokenService, PermissionResolver permissions, TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _permissions = permissions;
        _timeProvider = timeProvider;
    }

    /// <param name="user">Must have <c>UserRoles.Role</c> loaded.</param>
    public async Task<AuthResponse> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles.Select(x => x.Role).Where(r => r.IsActive).ToList();
        var permissions = await _permissions.GetPermissionsAsync(roles, cancellationToken);

        var tokens = _tokenService.Issue(user, roles.Select(r => r.Name).ToList(), permissions);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(tokens.RefreshToken),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAt = tokens.RefreshTokenExpiresAt
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            tokens.AccessToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAt,
            CurrentUserDto.From(UserDto.From(user), permissions));
    }
}
