using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Notifications.Features;
using CRM.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Notifications;

/// <summary>
/// In-app notifications built from other modules' events (task assigned / completed, deal won / lost).
/// Every signed-in user reads and marks their own; no permissions are needed.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => NotificationsDbContext.SchemaName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(NotificationsModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var notifications = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();

        notifications.MapGet("", async ([AsParameters] GetMyNotifications query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        notifications.MapGet("/unread-count", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetMyUnreadCount(), ct)));

        notifications.MapPost("/{id:int}/read", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkNotificationRead(id), ct);
            return TypedResults.NoContent();
        });

        notifications.MapPost("/read-all", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkAllNotificationsRead(), ct);
            return TypedResults.NoContent();
        });
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<NotificationsDbContext>(scope.ServiceProvider, cancellationToken);
    }

    // Demo notifications come from the demo data of Tasks and Sales: their events are delivered here like any others.
}
