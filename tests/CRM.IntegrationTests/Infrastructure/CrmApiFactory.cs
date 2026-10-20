using System.Collections.Concurrent;
using CRM.BuildingBlocks.Messaging;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Tasks.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CRM.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API against a real, throwaway PostgreSQL database:
/// <list type="bullet">
/// <item>by default a Testcontainers PostgreSQL container (needs Docker; this is what CI uses);</item>
/// <item>if <c>CRM_TEST_CONNECTION</c> is set, a temporary database on that server, dropped afterwards.</item>
/// </list>
/// Migrations and seeding run through the normal module initialization.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin-Passw0rd!";

    private PostgreSqlContainer? _container;
    private string? _serverConnectionString;
    private string? _databaseName;
    private string _connectionString = string.Empty;

    /// <summary>Whether modules seed their demo data at startup (off for the shared fixture).</summary>
    protected virtual bool SeedDemoData => false;

    /// <summary>Temporary folder for uploaded files, removed with the factory.</summary>
    public string AttachmentsPath { get; } = Path.Combine(Path.GetTempPath(), $"crm-test-files-{Guid.NewGuid():N}");

    /// <summary>Upload limit in the tests (small, so the size check is cheap to exercise).</summary>
    public const int MaxFileSizeMb = 1;

    /// <summary>Integration events delivered in-process by the outbox, for assertions.</summary>
    public ConcurrentQueue<IIntegrationEvent> DeliveredEvents { get; } = new();

    public async Task InitializeAsync()
    {
        _serverConnectionString = Environment.GetEnvironmentVariable("CRM_TEST_CONNECTION");

        if(_serverConnectionString is not null)
        {
            _databaseName = $"crm_test_{Guid.NewGuid():N}"[..21];
            await ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
            _connectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        // Starts the host: migrations, seeding and permission sync run here.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("JwtSettings:Issuer", "CRM.API");
        builder.UseSetting("JwtSettings:Audience", "CRM.CLIENT");
        builder.UseSetting("JwtSettings:SecretKey", "integration-tests-secret-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Outbox:PollingIntervalSeconds", "0.2");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "10000");
        builder.UseSetting("Seed:DemoData", SeedDemoData ? "true" : "false");
        builder.UseSetting("Attachments:StoragePath", AttachmentsPath);
        builder.UseSetting("Attachments:MaxFileSizeMb", MaxFileSizeMb.ToString());

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services.AddScoped<IIntegrationEventHandler<CompanyCreated>, RecordingHandler<CompanyCreated>>();
            services.AddScoped<IIntegrationEventHandler<TaskAssigned>, RecordingHandler<TaskAssigned>>();
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();

        if(_container is not null)
        {
            await _container.DisposeAsync();
        }

        if(Directory.Exists(AttachmentsPath))
        {
            Directory.Delete(AttachmentsPath, recursive: true);
        }

        if(_databaseName is not null)
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
        }
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class RecordingHandler<TEvent>(CrmApiFactory factory) : IIntegrationEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
        {
            factory.DeliveredEvents.Enqueue(integrationEvent);
            return Task.CompletedTask;
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CrmApiFactory>
{
    public const string Name = "api";
}
