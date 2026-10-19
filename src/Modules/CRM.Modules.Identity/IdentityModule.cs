using System.Text;
using System.Threading.RateLimiting;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Identity.Endpoints;
using CRM.Modules.Identity.Features.Auth;
using CRM.Modules.Identity.Infrastructure.Persistence;
using CRM.Modules.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CRM.Modules.Identity;

/// <summary>Users, roles and authentication (JWT access tokens + rotating refresh tokens).</summary>
public sealed class IdentityModule : IModule
{
    public string Name => IdentityDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => IdentityPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(IdentityModule).Assembly);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.HasValidKey,
                "JwtSettings:SecretKey must be set (at least 32 bytes). Use user-secrets or the JwtSettings__SecretKey environment variable.")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<AuthSessionFactory>();
        services.AddScoped<PermissionResolver>();

        // This module issues the tokens, so it also configures how they are validated.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                // Keep the token's short claim names (sub, role, ...) instead of mapping them to ClaimTypes URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SecretKey)),
                    NameClaimType = CrmClaimTypes.Name,
                    RoleClaimType = CrmClaimTypes.Role,
                    ClockSkew = TimeSpan.Zero
                };
            });

        // Brute-force protection for anonymous auth endpoints: per client IP, per minute.
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy(IdentityEndpoints.AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => IdentityEndpoints.Map(api);

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<IdentityDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<IdentityModule>();

        await ModuleInitialization.MigrateIfEnabledAsync<IdentityDbContext>(provider, cancellationToken);

        // Idempotent: built-in roles always, the admin user only when Seed:AdminEmail/Password are set.
        await IdentitySeeder.SeedAsync(
            db,
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<IPasswordHasher>(),
            logger,
            cancellationToken);

        // Keep the permission table in line with what the modules declare.
        await PermissionSynchronizer.SyncAsync(db, provider.GetRequiredService<ModuleCatalog>(), logger, cancellationToken);
    }
}
