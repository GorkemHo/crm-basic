using CRM.BuildingBlocks.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.BuildingBlocks.Modules;

/// <summary>
/// Entry point of a module. The host only knows modules through this contract:
/// it calls <see cref="Register"/> at startup, <see cref="MapEndpoints"/> when building the pipeline,
/// and <see cref="InitializeAsync"/> once the app is built (Development: migrations and seed data).
/// </summary>
public interface IModule
{
    /// <summary>Short lowercase name, also used as database schema and route prefix (e.g. <c>crm</c>).</summary>
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    /// <param name="api">The <c>/api</c> route group.</param>
    void MapEndpoints(IEndpointRouteBuilder api);

    /// <summary>Permissions this module checks; collected into the role editor and granted to Admin.</summary>
    IEnumerable<PermissionDefinition> Permissions => [];

    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>The modules composed into the host, available from DI.</summary>
public sealed class ModuleCatalog
{
    public ModuleCatalog(IReadOnlyList<IModule> modules)
    {
        Modules = modules;
        Permissions = modules.SelectMany(m => m.Permissions).ToList();

        var duplicate = Permissions.GroupBy(p => p.Name).FirstOrDefault(g => g.Count() > 1);
        if(duplicate is not null)
        {
            throw new InvalidOperationException($"Permission '{duplicate.Key}' is declared by more than one module.");
        }
    }

    public IReadOnlyList<IModule> Modules { get; }

    public IReadOnlyList<PermissionDefinition> Permissions { get; }
}
