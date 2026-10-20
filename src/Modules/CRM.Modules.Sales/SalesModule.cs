using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Sales.Features;
using CRM.Modules.Sales.Features.Deals;
using CRM.Modules.Sales.Features.Stages;
using CRM.Modules.Sales.Infrastructure;
using CRM.Modules.Sales.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Sales;

/// <summary>Deals and the sales pipeline.</summary>
public sealed class SalesModule : IModule
{
    public string Name => SalesDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => SalesPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SalesDbContext>(configuration, SalesDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(SalesModule).Assembly);
        services.AddScoped<DealNames>();
        services.AddScoped<IReferenceCheck, SalesReferenceCheck>();
        services.AddScoped<ISalesDirectory, SalesDirectory>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var stages = api.MapGroup("/pipeline-stages").WithTags("Pipeline stages");

        stages.MapGet("", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetStages(), ct)))
            .RequirePermission(SalesPermissions.DealsRead);

        stages.MapPost("", async Task<Created<StageDto>> (CreateStage request, ISender sender, CancellationToken ct) =>
        {
            var stage = await sender.Send(request, ct);
            return TypedResults.Created($"/api/pipeline-stages/{stage.Id}", stage);
        })
            .RequirePermission(SalesPermissions.StagesManage);

        stages.MapPatch("/{id:int}", async (int id, UpdateStageBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateStage(id, body.Name, body.Probability), ct)))
            .RequirePermission(SalesPermissions.StagesManage);

        stages.MapPut("/order", async (ReorderStages request, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(request, ct)))
            .RequirePermission(SalesPermissions.StagesManage);

        stages.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteStage(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(SalesPermissions.StagesManage);

        var deals = api.MapGroup("/deals").WithTags("Deals");

        deals.MapGet("", async ([AsParameters] GetDeals query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(SalesPermissions.DealsRead);

        deals.MapGet("/board", async ([AsParameters] GetBoard query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(SalesPermissions.DealsRead);

        deals.MapGet("/summary", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetPipelineSummary(), ct)))
            .RequirePermission(SalesPermissions.DealsRead);

        deals.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetDeal(id), ct)))
            .RequirePermission(SalesPermissions.DealsRead);

        deals.MapPost("", async Task<Created<DealDto>> (CreateDeal request, ISender sender, CancellationToken ct) =>
        {
            var deal = await sender.Send(request, ct);
            return TypedResults.Created($"/api/deals/{deal.Id}", deal);
        })
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapPut("/{id:int}", async (int id, UpdateDealBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(
                new UpdateDeal(id, body.Title, body.CompanyId, body.CustomerId, body.Amount, body.Currency, body.ExpectedCloseDate), ct)))
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapPost("/{id:int}/move", async (int id, MoveDealBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new MoveDeal(id, body.StageId), ct)))
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapPost("/{id:int}/win", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new WinDeal(id), ct)))
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapPost("/{id:int}/lose", async (int id, LoseDealBody? body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new LoseDeal(id, body?.Reason), ct)))
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapPost("/{id:int}/reopen", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ReopenDeal(id), ct)))
            .RequirePermission(SalesPermissions.DealsWrite);

        deals.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteDeal(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(SalesPermissions.DealsDelete);

        deals.MapGet("/{id:int}/history", async ([AsParameters] GetDealHistory query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(SalesPermissions.DealsRead);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<SalesDbContext>(scope.ServiceProvider, cancellationToken);
        await DefaultStages.EnsureAsync(scope.ServiceProvider.GetRequiredService<SalesDbContext>(), cancellationToken);
    }

    public Task SeedDemoDataAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        SalesDemoData.SeedAsync(scopedServices, cancellationToken);

    internal sealed record UpdateStageBody(string? Name, int? Probability);

    internal sealed record UpdateDealBody(
        string Title, int CompanyId, int? CustomerId, decimal Amount, string Currency, DateOnly? ExpectedCloseDate);

    internal sealed record MoveDealBody(int StageId);

    internal sealed record LoseDealBody(string? Reason);
}
