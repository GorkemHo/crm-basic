using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Tasks.Contracts;
using CRM.Modules.Tasks.Features;
using CRM.Modules.Tasks.Infrastructure;
using CRM.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Tasks;

/// <summary>To-dos assigned to users, optionally about a company, contact or deal.</summary>
public sealed class TasksModule : IModule
{
    public string Name => TasksDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => TasksPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TasksDbContext>(configuration, TasksDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(TasksModule).Assembly);
        services.AddScoped<TaskNames>();
        services.AddScoped<TaskRules>();
        services.AddScoped<IReferenceCheck, TasksReferenceCheck>();
        services.AddScoped<ITaskReports, TaskReports>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var tasks = api.MapGroup("/tasks").WithTags("Tasks");

        tasks.MapGet("", async ([AsParameters] GetTasks query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(TasksPermissions.Read);

        tasks.MapGet("/assignees", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetAssignees(), ct)))
            .RequirePermission(TasksPermissions.Write);

        tasks.MapGet("/my-summary", async (DateOnly? today, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetMyTaskSummary(today), ct)))
            .RequirePermission(TasksPermissions.Read);

        tasks.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetTask(id), ct)))
            .RequirePermission(TasksPermissions.Read);

        tasks.MapPost("", async Task<Created<TaskDto>> (CreateTask request, ISender sender, CancellationToken ct) =>
        {
            var task = await sender.Send(request, ct);
            return TypedResults.Created($"/api/tasks/{task.Id}", task);
        })
            .RequirePermission(TasksPermissions.Write);

        tasks.MapPut("/{id:int}", async (int id, TaskBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateTask(
                id, body.Title, body.Description, body.DueDate, body.Priority, body.AssigneeId,
                body.CompanyId, body.CustomerId, body.DealId), ct)))
            .RequirePermission(TasksPermissions.Write);

        tasks.MapPost("/{id:int}/complete", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new CompleteTask(id), ct)))
            .RequirePermission(TasksPermissions.Write);

        tasks.MapPost("/{id:int}/reopen", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ReopenTask(id), ct)))
            .RequirePermission(TasksPermissions.Write);

        tasks.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteTask(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(TasksPermissions.Write);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<TasksDbContext>(scope.ServiceProvider, cancellationToken);
    }

    public Task SeedDemoDataAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        TasksDemoData.SeedAsync(scopedServices, cancellationToken);

    internal sealed record TaskBody(
        string Title, string? Description, DateOnly? DueDate, string Priority, int? AssigneeId,
        int? CompanyId, int? CustomerId, int? DealId);
}
