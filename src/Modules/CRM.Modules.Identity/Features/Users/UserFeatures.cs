using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Features.Users;

// ---------------------------------------------------------------- List / get

internal sealed class GetUsers : PaginationRequest, IQuery<PagedResponse<UserDto>>
{
    /// <summary>Matches first name, last name or email (case-insensitive).</summary>
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public int? RoleId { get; set; }
}

internal sealed class GetUsersHandler : IRequestHandler<GetUsers, PagedResponse<UserDto>>
{
    private readonly IdentityDbContext _db;

    public GetUsersHandler(IdentityDbContext db) => _db = db;

    public Task<PagedResponse<UserDto>> Handle(GetUsers request, CancellationToken cancellationToken)
    {
        var query = _db.Users.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern) ||
                EF.Functions.ILike(x.LastName, pattern) ||
                EF.Functions.ILike(x.Email, pattern));
        }

        if(request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if(request.RoleId.HasValue)
        {
            query = query.Where(x => x.UserRoles.Any(r => r.RoleId == request.RoleId.Value));
        }

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName);
        }

        return query.ToPagedResponseAsync(request, UserDto.Projection, cancellationToken);
    }
}

internal sealed record GetUser(int Id) : IQuery<UserDto>;

internal sealed class GetUserHandler : IRequestHandler<GetUser, UserDto>
{
    private readonly IdentityDbContext _db;

    public GetUserHandler(IdentityDbContext db) => _db = db;

    public async Task<UserDto> Handle(GetUser request, CancellationToken cancellationToken) =>
        await _db.Users.Where(x => x.Id == request.Id).Select(UserDto.Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("User", request.Id);
}

// ---------------------------------------------------------------- Create

internal sealed record CreateUser(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PreferredLanguage,
    IReadOnlyList<int>? RoleIds) : ICommand<UserDto>;

internal sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.PreferredLanguage!).Must(Languages.Supported.Contains)
            .When(x => x.PreferredLanguage is not null)
            .WithMessage($"Supported languages: {string.Join(", ", Languages.Supported)}.");
    }
}

internal sealed class CreateUserHandler : IRequestHandler<CreateUser, UserDto>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserHandler(IdentityDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> Handle(CreateUser request, CancellationToken cancellationToken)
    {
        await UserRules.EnsureEmailIsFreeAsync(_db, request.Email, excludeUserId: null, cancellationToken);

        var roles = await UserRules.LoadRolesAsync(_db, request.RoleIds ?? [], cancellationToken);

        if(roles.Count == 0)
        {
            roles = [await _db.Roles.SingleAsync(x => x.Name == SystemRoles.User, cancellationToken)];
        }

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            PreferredLanguage = request.PreferredLanguage ?? Languages.Default
        };

        foreach(var role in roles)
        {
            user.UserRoles.Add(new UserRole { Role = role });
        }

        _db.Users.Add(user);
        await _db.SaveChangesAndPublishAsync(
            () => new UserRegistered(user.Id, user.Email, user.FullName, user.PreferredLanguage), cancellationToken);

        return UserDto.From(user);
    }
}

// ---------------------------------------------------------------- Update (partial)

/// <summary>Only non-null fields are changed.</summary>
internal sealed record UpdateUser(
    int Id,
    string? FirstName,
    string? LastName,
    string? Email,
    bool? IsActive,
    string? PreferredLanguage) : ICommand<UserDto>;

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUser>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FirstName!).NotEmpty().MaximumLength(100).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName!).NotEmpty().MaximumLength(100).When(x => x.LastName is not null);
        RuleFor(x => x.Email!).NotEmpty().EmailAddress().MaximumLength(150).When(x => x.Email is not null);
        RuleFor(x => x.PreferredLanguage!).Must(Languages.Supported.Contains)
            .When(x => x.PreferredLanguage is not null)
            .WithMessage($"Supported languages: {string.Join(", ", Languages.Supported)}.");
    }
}

internal sealed class UpdateUserHandler : IRequestHandler<UpdateUser, UserDto>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateUserHandler(IdentityDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(UpdateUser request, CancellationToken cancellationToken)
    {
        var user = await UserRules.LoadWithRolesAsync(_db, request.Id, cancellationToken);

        if(request.Email is not null && request.Email != user.Email)
        {
            await UserRules.EnsureEmailIsFreeAsync(_db, request.Email, user.Id, cancellationToken);
            user.Email = request.Email;
        }

        if(request.IsActive == false && user.Id == _currentUser.Id)
        {
            throw new BusinessRuleException("You cannot deactivate your own account.", "user.cannot_deactivate_self");
        }

        if(request.FirstName is not null) user.FirstName = request.FirstName;
        if(request.LastName is not null) user.LastName = request.LastName;
        if(request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        if(request.PreferredLanguage is not null) user.PreferredLanguage = request.PreferredLanguage;

        await _db.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}

// ---------------------------------------------------------------- Delete

internal sealed record DeleteUser(int Id) : ICommand;

internal sealed class DeleteUserHandler : IRequestHandler<DeleteUser, Unit>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IEnumerable<IReferenceCheck> _referenceChecks;

    public DeleteUserHandler(IdentityDbContext db, ICurrentUser currentUser, IEnumerable<IReferenceCheck> referenceChecks)
    {
        _db = db;
        _currentUser = currentUser;
        _referenceChecks = referenceChecks;
    }

    public async Task<Unit> Handle(DeleteUser request, CancellationToken cancellationToken)
    {
        if(request.Id == _currentUser.Id)
        {
            throw new BusinessRuleException("You cannot delete your own account.", "user.cannot_delete_self");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        await _referenceChecks.EnsureNotReferencedAsync(IdentityResources.User, user.Id, cancellationToken);

        // Soft delete (see ISoftDeletable); also end the user's sessions.
        _db.Users.Remove(user);
        _db.AddIntegrationEvent(new UserDeleted(user.Id));
        foreach(var token in await _db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync(cancellationToken))
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------- Roles of a user

internal sealed record AssignUserRoles(int UserId, IReadOnlyList<int> RoleIds) : ICommand<UserDto>;

internal sealed class AssignUserRolesValidator : AbstractValidator<AssignUserRoles>
{
    public AssignUserRolesValidator() => RuleFor(x => x.RoleIds).NotNull();
}

internal sealed class AssignUserRolesHandler : IRequestHandler<AssignUserRoles, UserDto>
{
    private readonly IdentityDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AssignUserRolesHandler(IdentityDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(AssignUserRoles request, CancellationToken cancellationToken)
    {
        var user = await UserRules.LoadWithRolesAsync(_db, request.UserId, cancellationToken);
        var roles = await UserRules.LoadRolesAsync(_db, request.RoleIds, cancellationToken);

        if(user.Id == _currentUser.Id && roles.All(r => r.Name != SystemRoles.Admin))
        {
            throw new BusinessRuleException("You cannot remove the Admin role from yourself.", "user.cannot_remove_own_admin");
        }

        // Replace the set; one SaveChanges keeps it atomic.
        user.UserRoles.Clear();
        foreach(var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, Role = role });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}

// ---------------------------------------------------------------- Shared rules

internal static class UserRules
{
    public static async Task<User> LoadWithRolesAsync(IdentityDbContext db, int id, CancellationToken cancellationToken) =>
        await db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("User", id);

    public static async Task EnsureEmailIsFreeAsync(IdentityDbContext db, string email, int? excludeUserId, CancellationToken cancellationToken)
    {
        if(await db.Users.AnyAsync(x => x.Email == email && x.Id != excludeUserId, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.", "user.email_taken");
        }
    }

    public static async Task<List<Role>> LoadRolesAsync(IdentityDbContext db, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();
        var roles = await db.Roles.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

        var missing = ids.Except(roles.Select(x => x.Id)).ToList();
        if(missing.Count > 0)
        {
            throw new NotFoundException($"Role(s) not found: {string.Join(", ", missing)}.", "role.not_found");
        }

        return roles;
    }
}
