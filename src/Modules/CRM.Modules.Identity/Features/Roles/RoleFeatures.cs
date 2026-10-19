using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;
using FluentValidation;
using ValidationException = CRM.BuildingBlocks.Exceptions.ValidationException;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Features.Roles;

internal sealed record GetRoles : IQuery<IReadOnlyList<RoleDto>>;

internal sealed class GetRolesHandler : IRequestHandler<GetRoles, IReadOnlyList<RoleDto>>
{
    private readonly IdentityDbContext _db;
    private readonly PermissionResolver _permissions;

    public GetRolesHandler(IdentityDbContext db, PermissionResolver permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RoleDto>> Handle(GetRoles request, CancellationToken cancellationToken)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(x => x.Name).Select(RoleDto.Projection).ToListAsync(cancellationToken);
        return roles.Select(r => r.WithAdminPermissions(_permissions.AllPermissions)).ToList();
    }
}

internal sealed record GetRole(int Id) : IQuery<RoleDto>;

internal sealed class GetRoleHandler : IRequestHandler<GetRole, RoleDto>
{
    private readonly IdentityDbContext _db;
    private readonly PermissionResolver _permissions;

    public GetRoleHandler(IdentityDbContext db, PermissionResolver permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RoleDto> Handle(GetRole request, CancellationToken cancellationToken) =>
        await RoleRules.GetDtoAsync(_db, _permissions, request.Id, cancellationToken);
}

// ---------------------------------------------------------------- Permissions

internal sealed record GetPermissions : IQuery<IReadOnlyList<PermissionDto>>;

internal sealed class GetPermissionsHandler : IRequestHandler<GetPermissions, IReadOnlyList<PermissionDto>>
{
    private readonly IdentityDbContext _db;

    public GetPermissionsHandler(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<PermissionDto>> Handle(GetPermissions request, CancellationToken cancellationToken) =>
        await _db.Permissions.AsNoTracking()
            .OrderBy(x => x.Module).ThenBy(x => x.Group).ThenBy(x => x.Name)
            .Select(x => new PermissionDto(x.Name, x.Module, x.Group, x.Description))
            .ToListAsync(cancellationToken);
}

/// <summary>Replaces the role's permissions. Takes effect for users at their next sign-in or token refresh.</summary>
internal sealed record SetRolePermissions(int RoleId, IReadOnlyList<string> Permissions) : ICommand<RoleDto>;

internal sealed class SetRolePermissionsValidator : AbstractValidator<SetRolePermissions>
{
    public SetRolePermissionsValidator() => RuleFor(x => x.Permissions).NotNull();
}

internal sealed class SetRolePermissionsHandler : IRequestHandler<SetRolePermissions, RoleDto>
{
    private readonly IdentityDbContext _db;
    private readonly PermissionResolver _permissions;

    public SetRolePermissionsHandler(IdentityDbContext db, PermissionResolver permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RoleDto> Handle(SetRolePermissions request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", request.RoleId);

        if(role.Name == SystemRoles.Admin)
        {
            throw new BusinessRuleException("The Admin role always has every permission.", "role.admin_permissions_fixed");
        }

        var requested = request.Permissions.Distinct().ToList();
        var known = await _db.Permissions.Where(x => requested.Contains(x.Name)).Select(x => x.Name).ToListAsync(cancellationToken);
        var unknown = requested.Except(known).ToList();

        if(unknown.Count > 0)
        {
            throw new ValidationException("permissions", $"Unknown permission(s): {string.Join(", ", unknown)}.", "permission.unknown");
        }

        role.Permissions.Clear();
        foreach(var name in requested)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionName = name });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await RoleRules.GetDtoAsync(_db, _permissions, role.Id, cancellationToken);
    }
}

internal sealed record CreateRole(string Name) : ICommand<RoleDto>;

internal sealed class CreateRoleValidator : AbstractValidator<CreateRole>
{
    public CreateRoleValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

internal sealed class CreateRoleHandler : IRequestHandler<CreateRole, RoleDto>
{
    private readonly IdentityDbContext _db;
    private readonly PermissionResolver _permissions;

    public CreateRoleHandler(IdentityDbContext db, PermissionResolver permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RoleDto> Handle(CreateRole request, CancellationToken cancellationToken)
    {
        await RoleRules.EnsureNameIsFreeAsync(_db, request.Name, excludeRoleId: null, cancellationToken);

        var role = new Role { Name = request.Name.Trim() };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        return await RoleRules.GetDtoAsync(_db, _permissions, role.Id, cancellationToken);
    }
}

/// <summary>Only non-null fields are changed.</summary>
internal sealed record UpdateRole(int Id, string? Name, bool? IsActive) : ICommand<RoleDto>;

internal sealed class UpdateRoleValidator : AbstractValidator<UpdateRole>
{
    public UpdateRoleValidator() => RuleFor(x => x.Name!).NotEmpty().MaximumLength(50).When(x => x.Name is not null);
}

internal sealed class UpdateRoleHandler : IRequestHandler<UpdateRole, RoleDto>
{
    private readonly IdentityDbContext _db;
    private readonly PermissionResolver _permissions;

    public UpdateRoleHandler(IdentityDbContext db, PermissionResolver permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RoleDto> Handle(UpdateRole request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Role", request.Id);

        var renaming = request.Name is not null && request.Name != role.Name;

        if(SystemRoles.IsSystemRole(role.Name) && (renaming || request.IsActive == false))
        {
            throw new BusinessRuleException("Built-in roles cannot be renamed or deactivated.", "role.system_role");
        }

        if(renaming)
        {
            await RoleRules.EnsureNameIsFreeAsync(_db, request.Name!, role.Id, cancellationToken);
            role.Name = request.Name!.Trim();
        }

        if(request.IsActive.HasValue) role.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(cancellationToken);

        return await RoleRules.GetDtoAsync(_db, _permissions, role.Id, cancellationToken);
    }
}

internal sealed record DeleteRole(int Id) : ICommand;

internal sealed class DeleteRoleHandler : IRequestHandler<DeleteRole, Unit>
{
    private readonly IdentityDbContext _db;

    public DeleteRoleHandler(IdentityDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteRole request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Role", request.Id);

        if(SystemRoles.IsSystemRole(role.Name))
        {
            throw new BusinessRuleException("Built-in roles cannot be deleted.", "role.system_role");
        }

        if(await _db.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken))
        {
            throw new ConflictException("The role is assigned to users and cannot be deleted.", "role.in_use");
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal static class RoleRules
{
    public static async Task<RoleDto> GetDtoAsync(IdentityDbContext db, PermissionResolver permissions, int id, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking().Where(x => x.Id == id).Select(RoleDto.Projection).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Role", id);

        return role.WithAdminPermissions(permissions.AllPermissions);
    }

    public static async Task EnsureNameIsFreeAsync(IdentityDbContext db, string name, int? excludeRoleId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();

        if(await db.Roles.AnyAsync(x => x.Name.ToLower() == normalized && x.Id != excludeRoleId, cancellationToken))
        {
            throw new ConflictException("A role with the same name already exists.", "role.name_taken");
        }
    }
}
