using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Attachments.Contracts;
using CRM.Modules.Attachments.Features;
using CRM.Modules.Attachments.Infrastructure;
using CRM.Modules.Attachments.Infrastructure.Persistence;
using CRM.Modules.Attachments.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Attachments;

/// <summary>Files attached to companies, customers and deals, kept in a file store (the server's disk by default).</summary>
public sealed class AttachmentsModule : IModule
{
    public string Name => AttachmentsDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => AttachmentsPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AttachmentsDbContext>(configuration, AttachmentsDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(AttachmentsModule).Assembly);

        services.AddOptions<AttachmentOptions>()
            .Bind(configuration.GetSection(AttachmentOptions.SectionName))
            .Validate(o => o.MaxFileSizeMb is > 0 and <= 1024, "Attachments:MaxFileSizeMb must be between 1 and 1024.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.StoragePath), "Attachments:StoragePath must be set.")
            .ValidateOnStart();

        // The server's own body limits (Kestrel 30 MB, multipart 128 MB by default) must leave room for the
        // largest allowed file plus the multipart envelope; the exact limit is enforced by the upload handler.
        var maxFileBytes = configuration.GetSection(AttachmentOptions.SectionName).Get<AttachmentOptions>()?.MaxFileSizeBytes
            ?? new AttachmentOptions().MaxFileSizeBytes;
        var bodyLimit = maxFileBytes + 1024 * 1024;
        services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = Math.Max(o.Limits.MaxRequestBodySize ?? 0, bodyLimit));
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = Math.Max(o.MultipartBodyLengthLimit, bodyLimit));

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<AttachmentRules>();
        services.AddScoped<IReferenceCheck, AttachmentsReferenceCheck>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var attachments = api.MapGroup("/attachments").WithTags("Attachments");

        attachments.MapGet("", async (string recordType, int recordId, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetAttachments(recordType, recordId), ct)))
            .RequirePermission(AttachmentsPermissions.Read);

        // multipart/form-data: recordType, recordId, file. Bearer tokens (no cookies), so no antiforgery token.
        attachments.MapPost("", async Task<Created<AttachmentDto>> (
                [Microsoft.AspNetCore.Mvc.FromForm] string recordType,
                [Microsoft.AspNetCore.Mvc.FromForm] int recordId,
                IFormFile file,
                ISender sender,
                CancellationToken ct) =>
            {
                await using var content = file.OpenReadStream();
                var attachment = await sender.Send(new UploadAttachment(recordType, recordId, file.FileName, file.Length, content), ct);
                return TypedResults.Created($"/api/attachments/{attachment.Id}", attachment);
            })
            .DisableAntiforgery()
            .RequirePermission(AttachmentsPermissions.Write);

        attachments.MapGet("/{id:int}/download", async (int id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var file = await sender.Send(new DownloadAttachment(id), ct);
                // Always a download, never rendered by the browser as a page.
                http.Response.Headers.XContentTypeOptions = "nosniff";
                return TypedResults.File(file.Content, file.ContentType, file.FileName);
            })
            .RequirePermission(AttachmentsPermissions.Read);

        attachments.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteAttachment(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(AttachmentsPermissions.Write);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<AttachmentsDbContext>(scope.ServiceProvider, cancellationToken);
    }

    public Task SeedDemoDataAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        AttachmentsDemoData.SeedAsync(scopedServices, cancellationToken);
}
