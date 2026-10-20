using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Activities.Contracts;
using CRM.Modules.Activities.Features;
using CRM.Modules.Activities.Infrastructure;
using CRM.Modules.Activities.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Activities;

/// <summary>Notes, calls, meetings and emails logged on companies, contacts and deals.</summary>
public sealed class ActivitiesModule : IModule
{
    public string Name => ActivitiesDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => ActivitiesPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ActivitiesDbContext>(configuration, ActivitiesDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(ActivitiesModule).Assembly);
        services.AddScoped<ActivityNames>();
        services.AddScoped<ActivityRules>();
        services.AddScoped<IReferenceCheck, ActivitiesReferenceCheck>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var activities = api.MapGroup("/activities").WithTags("Activities");

        activities.MapGet("", async ([AsParameters] GetActivities query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(ActivitiesPermissions.Read);

        activities.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetActivity(id), ct)))
            .RequirePermission(ActivitiesPermissions.Read);

        activities.MapPost("", async Task<Created<ActivityDto>> (LogActivity request, ISender sender, CancellationToken ct) =>
        {
            var activity = await sender.Send(request, ct);
            return TypedResults.Created($"/api/activities/{activity.Id}", activity);
        })
            .RequirePermission(ActivitiesPermissions.Write);

        activities.MapPut("/{id:int}", async (int id, ActivityBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateActivity(
                id, body.Type, body.Subject, body.Notes, body.OccurredAt, body.DurationMinutes,
                body.CompanyId, body.CustomerId, body.DealId), ct)))
            .RequirePermission(ActivitiesPermissions.Write);

        activities.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteActivity(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(ActivitiesPermissions.Write);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<ActivitiesDbContext>(scope.ServiceProvider, cancellationToken);
    }

    public Task SeedDemoDataAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        ActivitiesDemoData.SeedAsync(scopedServices, cancellationToken);

    internal sealed record ActivityBody(
        string Type, string Subject, string? Notes, DateTime OccurredAt, int? DurationMinutes,
        int CompanyId, int? CustomerId, int? DealId);
}
