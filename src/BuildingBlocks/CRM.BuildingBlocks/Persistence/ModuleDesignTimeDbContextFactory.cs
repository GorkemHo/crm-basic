using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRM.BuildingBlocks.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create a module's DbContext without starting the host.
/// Subclass it in the module (one line) and run, for example:
/// <code>dotnet ef migrations add Name --project src/Modules/CRM.Modules.Crm --output-dir Infrastructure/Persistence/Migrations</code>
/// The connection string is only needed for commands that touch the database
/// (<c>CRM_DESIGN_CONNECTION</c> environment variable, otherwise a local default).
/// </summary>
public abstract class ModuleDesignTimeDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    protected abstract string Schema { get; }

    public TContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CRM_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=CRMDB;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }
}
