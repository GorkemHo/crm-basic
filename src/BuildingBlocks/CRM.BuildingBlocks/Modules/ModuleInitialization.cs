using CRM.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CRM.BuildingBlocks.Modules;

public static class ModuleInitialization
{
    /// <summary>
    /// <c>Database:MigrateOnStartup</c>; defaults to true in Development only. Production deployments
    /// normally apply migrations as a separate step (e.g. an EF migration bundle).
    /// </summary>
    public static bool ShouldMigrateOnStartup(IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>().GetValue(
            "Database:MigrateOnStartup",
            services.GetRequiredService<IHostEnvironment>().IsDevelopment());

    /// <summary>Applies the module's pending migrations when <see cref="ShouldMigrateOnStartup"/> allows it.</summary>
    public static async Task<bool> MigrateIfEnabledAsync<TContext>(IServiceProvider scopedServices, CancellationToken cancellationToken)
        where TContext : ModuleDbContext
    {
        if(!ShouldMigrateOnStartup(scopedServices))
        {
            return false;
        }

        await scopedServices.GetRequiredService<TContext>().Database.MigrateAsync(cancellationToken);
        return true;
    }
}
