using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;

namespace CRM.Modules.Identity.Features.Auth;

/// <summary>Issues a token pair for a user and stores the refresh token's hash.</summary>
internal sealed class AuthSessionFactory
{
    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AuthSessionFactory(IdentityDbContext db, ITokenService tokenService, TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    /// <param name="user">Must have <c>UserRoles.Role</c> loaded.</param>
    public async Task<AuthResponse> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles
            .Where(x => x.Role.IsActive)
            .Select(x => x.Role.Name)
            .ToList();

        var tokens = _tokenService.Issue(user, roles);

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
            UserDto.From(user));
    }
}
