using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;
using FluentValidation;
using ValidationException = CRM.BuildingBlocks.Exceptions.ValidationException;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Features.Auth;

// ---------------------------------------------------------------- Login

internal sealed record Login(string Email, string Password) : ICommand<AuthResponse>;

internal sealed class LoginValidator : AbstractValidator<Login>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

internal sealed class LoginHandler : IRequestHandler<Login, AuthResponse>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthSessionFactory _sessions;

    public LoginHandler(IdentityDbContext db, IPasswordHasher passwordHasher, AuthSessionFactory sessions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
    }

    public async Task<AuthResponse> Handle(Login request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == request.Email, cancellationToken);

        if(user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.", "auth.invalid_credentials");
        }

        if(!user.IsActive)
        {
            throw new UnauthorizedException("User account is disabled.", "auth.user_disabled");
        }

        return await _sessions.CreateAsync(user, cancellationToken);
    }
}

// ---------------------------------------------------------------- Register

internal sealed record Register(string FirstName, string LastName, string Email, string Password) : ICommand<UserDto>;

internal sealed class RegisterValidator : AbstractValidator<Register>
{
    public RegisterValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

internal sealed class RegisterHandler : IRequestHandler<Register, UserDto>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterHandler(IdentityDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> Handle(Register request, CancellationToken cancellationToken)
    {
        if(await _db.Users.AnyAsync(x => x.Email == request.Email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.", "user.email_taken");
        }

        // Self-registration always gets the default role; elevated roles are assigned by an admin.
        var defaultRole = await _db.Roles.SingleOrDefaultAsync(x => x.Name == SystemRoles.User, cancellationToken)
            ?? throw new InvalidOperationException($"Default role '{SystemRoles.User}' does not exist.");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };
        user.UserRoles.Add(new UserRole { Role = defaultRole });

        _db.Users.Add(user);
        await _db.SaveChangesAndPublishAsync(
            () => new UserRegistered(user.Id, user.Email, user.FullName, user.PreferredLanguage), cancellationToken);

        return UserDto.From(user);
    }
}

// ---------------------------------------------------------------- Refresh

internal sealed record RefreshTokens(string RefreshToken) : ICommand<AuthResponse>;

internal sealed class RefreshTokensValidator : AbstractValidator<RefreshTokens>
{
    public RefreshTokensValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

internal sealed class RefreshTokensHandler : IRequestHandler<RefreshTokens, AuthResponse>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly AuthSessionFactory _sessions;
    private readonly TimeProvider _timeProvider;

    public RefreshTokensHandler(IdentityDbContext db, ITokenService tokenService, AuthSessionFactory sessions, TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _sessions = sessions;
        _timeProvider = timeProvider;
    }

    public async Task<AuthResponse> Handle(RefreshTokens request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.", "auth.invalid_refresh_token");

        if(stored.IsRevoked)
        {
            // A revoked token presented again may have been stolen: end every session of that user.
            await _db.RefreshTokens
                .Where(x => x.UserId == stored.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);

            throw new UnauthorizedException("Invalid refresh token.", "auth.invalid_refresh_token");
        }

        if(stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException("Refresh token has expired.", "auth.refresh_token_expired");
        }

        if(!stored.User.IsActive)
        {
            throw new UnauthorizedException("User account is disabled.", "auth.user_disabled");
        }

        // Rotation: each refresh token works once.
        stored.RevokedAt = now;

        return await _sessions.CreateAsync(stored.User, cancellationToken);
    }
}

// ---------------------------------------------------------------- Logout

internal sealed record Logout(string RefreshToken) : ICommand;

internal sealed class LogoutHandler : IRequestHandler<Logout, Unit>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public LogoutHandler(IdentityDbContext db, ITokenService tokenService, TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Unit> Handle(Logout request, CancellationToken cancellationToken)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);

        await _db.RefreshTokens
            .Where(x => x.TokenHash == hash && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, _timeProvider.GetUtcNow().UtcDateTime), cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------- Me

internal sealed record GetCurrentUser : IQuery<CurrentUserDto>;

internal sealed class GetCurrentUserHandler : IRequestHandler<GetCurrentUser, CurrentUserDto>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly PermissionResolver _permissions;

    public GetCurrentUserHandler(IdentityDbContext db, ICurrentUser currentUser, PermissionResolver permissions)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<CurrentUserDto> Handle(GetCurrentUser request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedException();

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Fresh from the database, so it reflects role changes made after the token was issued.
        var permissions = await _permissions.GetPermissionsAsync(user.UserRoles.Select(x => x.Role).ToList(), cancellationToken);

        return CurrentUserDto.From(UserDto.From(user), permissions);
    }
}

internal sealed record UpdateMyPreferences(string PreferredLanguage) : ICommand<UserDto>;

internal sealed class UpdateMyPreferencesValidator : AbstractValidator<UpdateMyPreferences>
{
    public UpdateMyPreferencesValidator()
    {
        RuleFor(x => x.PreferredLanguage)
            .Must(lang => Languages.Supported.Contains(lang))
            .WithMessage($"Supported languages: {string.Join(", ", Languages.Supported)}.");
    }
}

internal sealed class UpdateMyPreferencesHandler : IRequestHandler<UpdateMyPreferences, UserDto>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateMyPreferencesHandler(IdentityDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(UpdateMyPreferences request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedException();

        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        user.PreferredLanguage = request.PreferredLanguage;
        await _db.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}

internal sealed record ChangeMyPassword(string CurrentPassword, string NewPassword) : ICommand;

internal sealed class ChangeMyPasswordValidator : AbstractValidator<ChangeMyPassword>
{
    public ChangeMyPasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must differ from the current one.");
    }
}

internal sealed class ChangeMyPasswordHandler : IRequestHandler<ChangeMyPassword, Unit>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public ChangeMyPasswordHandler(IdentityDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Unit> Handle(ChangeMyPassword request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedException();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        if(!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException("currentPassword", "The current password is incorrect.", "auth.wrong_password");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Sign out every other session.
        await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, _timeProvider.GetUtcNow().UtcDateTime), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
