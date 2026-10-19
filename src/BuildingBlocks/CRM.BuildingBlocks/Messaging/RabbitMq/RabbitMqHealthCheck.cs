using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CRM.BuildingBlocks.Messaging.RabbitMq;

/// <summary>Healthy when a connection to the broker can be opened (registered only while messaging is enabled).</summary>
internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnectionProvider _connectionProvider;

    public RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            return connection.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Connection is closed.");
        }
        catch(Exception exception)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to RabbitMQ.", exception);
        }
    }
}
