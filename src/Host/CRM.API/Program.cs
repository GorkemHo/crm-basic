using CRM.API.ErrorHandling;
using CRM.API.Extensions;
using CRM.API.Observability;
using CRM.BuildingBlocks.Modules;
using CRM.Modules.Activities;
using CRM.Modules.Crm;
using CRM.Modules.Identity;
using CRM.Modules.Sales;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Errors: every failure is returned as an RFC 7807 problem (application/problem+json).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsDefaults.Customize);
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// Modules: add new ones here.
builder.AddModules(
    new IdentityModule(),
    new CrmModule(),
    new SalesModule(),
    new ActivitiesModule());

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Validation messages follow Accept-Language (the UI sends the user's preferred language).
builder.Services.AddRequestLocalization(options =>
{
    string[] cultures = ["en", "tr"];
    options.SetDefaultCulture(cultures[0]).AddSupportedCultures(cultures).AddSupportedUICultures(cultures);
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

// Outermost, so it logs the status the client actually receives: expected errors (401, 404, 409...)
// are turned into problem responses by the exception handler below before this sees them.
app.UseSerilogRequestLogging(options =>
    // Probes hit health endpoints constantly; keep them out of the normal request log.
    options.GetLevel = (context, _, exception) =>
        exception is not null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information);
app.UseExceptionHandler();
// Bare status codes (401/403 from auth, 404 for unknown routes) also get a problem body.
app.UseStatusCodePages();

if(app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapModules();

await app.InitializeModulesAsync();

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
