using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Registers a module's DbContext on the shared PostgreSQL database, with its migrations history
    /// kept inside the module's schema so modules can be migrated independently.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                .UseSnakeCaseNamingConvention();

            options.AddInterceptors(serviceProvider.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>());
        });

        return services;
    }
}
