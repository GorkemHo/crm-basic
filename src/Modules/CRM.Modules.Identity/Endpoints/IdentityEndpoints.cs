using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Pagination;
using CRM.Modules.Identity.Features;
using CRM.Modules.Identity.Features.Auth;
using CRM.Modules.Identity.Features.Roles;
using CRM.Modules.Identity.Features.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CRM.Modules.Identity.Endpoints;

internal static class IdentityEndpoints
{
    public const string AuthRateLimitPolicy = "auth";

    public static void Map(IEndpointRouteBuilder api)
    {
        MapAuth(api.MapGroup("/auth").WithTags("Auth"));
        MapUsers(api.MapGroup("/users").WithTags("Users").RequireAuthorization(IdentityModule.AdminPolicy));
        MapRoles(api.MapGroup("/roles").WithTags("Roles").RequireAuthorization(IdentityModule.AdminPolicy));
    }

    private static void MapAuth(RouteGroupBuilder auth)
    {
        auth.MapPost("/login", async (Login request, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapPost("/register", async Task<Created<UserDto>> (Register request, ISender sender, CancellationToken ct) =>
            {
                var user = await sender.Send(request, ct);
                return TypedResults.Created($"/api/users/{user.Id}", user);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapPost("/refresh", async (RefreshTokens request, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapPost("/logout", async (Logout request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(request, ct);
                return TypedResults.NoContent();
            })
            .AllowAnonymous();

        var me = auth.MapGroup("/me").RequireAuthorization();

        me.MapGet("", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetCurrentUser(), ct)));

        me.MapPut("/preferences", async (UpdateMyPreferences request, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(request, ct)));

        me.MapPost("/change-password", async (ChangeMyPassword request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(request, ct);
            return TypedResults.NoContent();
        });
    }

    private static void MapUsers(RouteGroupBuilder users)
    {
        users.MapGet("", async ([AsParameters] GetUsers query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        users.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetUser(id), ct)));

        users.MapPost("", async Task<Created<UserDto>> (CreateUser request, ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(request, ct);
            return TypedResults.Created($"/api/users/{user.Id}", user);
        });

        users.MapPatch("/{id:int}", async (int id, UpdateUserBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(
                new UpdateUser(id, body.FirstName, body.LastName, body.Email, body.IsActive, body.PreferredLanguage), ct)));

        users.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteUser(id), ct);
            return TypedResults.NoContent();
        });

        users.MapGet("/{id:int}/history", async ([AsParameters] GetUserHistory query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        users.MapPut("/{id:int}/roles", async (int id, AssignRolesBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new AssignUserRoles(id, body.RoleIds), ct)));
    }

    private static void MapRoles(RouteGroupBuilder roles)
    {
        roles.MapGet("", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetRoles(), ct)));

        roles.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetRole(id), ct)));

        roles.MapPost("", async Task<Created<RoleDto>> (CreateRole request, ISender sender, CancellationToken ct) =>
        {
            var role = await sender.Send(request, ct);
            return TypedResults.Created($"/api/roles/{role.Id}", role);
        });

        roles.MapPatch("/{id:int}", async (int id, UpdateRoleBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateRole(id, body.Name, body.IsActive), ct)));

        roles.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRole(id), ct);
            return TypedResults.NoContent();
        });
    }

    internal sealed record UpdateUserBody(string? FirstName, string? LastName, string? Email, bool? IsActive, string? PreferredLanguage);

    internal sealed record AssignRolesBody(IReadOnlyList<int> RoleIds);

    internal sealed record UpdateRoleBody(string? Name, bool? IsActive);
}
