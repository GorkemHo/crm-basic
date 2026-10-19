using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.Modules.Identity.Domain;
using CRM.Modules.Identity.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Identity.Features.Roles;

internal sealed record GetRoles : IQuery<IReadOnlyList<RoleDto>>;

internal sealed class GetRolesHandler : IRequestHandler<GetRoles, IReadOnlyList<RoleDto>>
{
    private readonly IdentityDbContext _db;

    public GetRolesHandler(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> Handle(GetRoles request, CancellationToken cancellationToken) =>
        await _db.Roles.AsNoTracking().OrderBy(x => x.Name).Select(RoleDto.Projection).ToListAsync(cancellationToken);
}

internal sealed record GetRole(int Id) : IQuery<RoleDto>;

internal sealed class GetRoleHandler : IRequestHandler<GetRole, RoleDto>
{
    private readonly IdentityDbContext _db;

    public GetRoleHandler(IdentityDbContext db) => _db = db;

    public async Task<RoleDto> Handle(GetRole request, CancellationToken cancellationToken) =>
        await _db.Roles.Where(x => x.Id == request.Id).Select(RoleDto.Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Role", request.Id);
}

internal sealed record CreateRole(string Name) : ICommand<RoleDto>;

internal sealed class CreateRoleValidator : AbstractValidator<CreateRole>
{
    public CreateRoleValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

internal sealed class CreateRoleHandler : IRequestHandler<CreateRole, RoleDto>
{
    private readonly IdentityDbContext _db;

    public CreateRoleHandler(IdentityDbContext db) => _db = db;

    public async Task<RoleDto> Handle(CreateRole request, CancellationToken cancellationToken)
    {
        await RoleRules.EnsureNameIsFreeAsync(_db, request.Name, excludeRoleId: null, cancellationToken);

        var role = new Role { Name = request.Name.Trim() };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        return await _db.Roles.Where(x => x.Id == role.Id).Select(RoleDto.Projection).SingleAsync(cancellationToken);
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

    public UpdateRoleHandler(IdentityDbContext db) => _db = db;

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

        return await _db.Roles.Where(x => x.Id == role.Id).Select(RoleDto.Projection).SingleAsync(cancellationToken);
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
    public static async Task EnsureNameIsFreeAsync(IdentityDbContext db, string name, int? excludeRoleId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();

        if(await db.Roles.AnyAsync(x => x.Name.ToLower() == normalized && x.Id != excludeRoleId, cancellationToken))
        {
            throw new ConflictException("A role with the same name already exists.", "role.name_taken");
        }
    }
}
