using CRM.BuildingBlocks.Messaging;
using Microsoft.Extensions.Logging;

namespace CRM.BuildingBlocks.Messaging.RabbitMq;

/// <summary>Used while RabbitMq:Enabled is false: messages are dropped (logged at debug level).</summary>
public sealed class NullMessagePublisher : IMessagePublisher
{
    private readonly ILogger<NullMessagePublisher> _logger;

    public NullMessagePublisher(ILogger<NullMessagePublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class
    {
        _logger.LogDebug("Messaging disabled; dropped {MessageType} ({RoutingKey})",
            typeof(T).Name, routingKey ?? MessageRouting.For<T>());

        return Task.CompletedTask;
    }
}
