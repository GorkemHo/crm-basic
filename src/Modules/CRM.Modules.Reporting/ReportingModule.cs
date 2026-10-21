using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Reporting.Contracts;
using CRM.Modules.Reporting.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Reporting;

/// <summary>
/// Team-wide reports. Has no tables of its own: each module answers through the report interface in its
/// Contracts (ISalesReports, IActivityReports, ITaskReports); this module adds people's names and the endpoints.
/// Its demo data is the other modules' demo data.
/// </summary>
public sealed class ReportingModule : IModule
{
    public string Name => "reporting";

    public IEnumerable<PermissionDefinition> Permissions => ReportingPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddRequestHandlersFromAssembly(typeof(ReportingModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var reports = api.MapGroup("/reports").WithTags("Reports").RequirePermission(ReportingPermissions.Read);

        reports.MapGet("/sales", async ([AsParameters] GetSalesReport query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        reports.MapGet("/pipeline", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetPipelineReport(), ct)));

        reports.MapGet("/activities", async ([AsParameters] GetActivityReport query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        reports.MapGet("/tasks", async ([AsParameters] GetTaskReport query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));
    }
}
