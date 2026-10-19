using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Messaging.RabbitMq;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CRM.BuildingBlocks.Modules;

public static class ModuleHostExtensions
{
    /// <summary>Registers the given modules' services and makes the <see cref="ModuleCatalog"/> available.</summary>
    public static WebApplicationBuilder AddModules(this WebApplicationBuilder builder, params IModule[] modules)
    {
        builder.Services.AddDispatcher();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IInterceptor, AuditingInterceptor>();

        foreach(var module in modules)
        {
            module.Register(builder.Services, builder.Configuration);
        }

        // Outbox delivery publishes through this (a no-op unless RabbitMq:Enabled).
        builder.Services.AddMessaging(builder.Configuration);

        builder.Services.AddSingleton(new ModuleCatalog(modules));

        return builder;
    }

    /// <summary>Maps every module's endpoints under <c>/api</c>.</summary>
    public static WebApplication MapModules(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        foreach(var module in app.Services.GetRequiredService<ModuleCatalog>().Modules)
        {
            module.MapEndpoints(api);
        }

        return app;
    }

    /// <summary>Runs each module's initialization in registration order.</summary>
    public static async Task InitializeModulesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ModuleHostExtensions));

        foreach(var module in app.Services.GetRequiredService<ModuleCatalog>().Modules)
        {
            logger.LogInformation("Initializing module {Module}", module.Name);
            await module.InitializeAsync(app.Services, cancellationToken);
        }
    }
}
