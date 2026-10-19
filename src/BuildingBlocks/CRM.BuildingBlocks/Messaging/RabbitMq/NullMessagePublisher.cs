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

    public Task PublishAsync(object message, Type messageType, string? routingKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Messaging disabled; dropped {MessageType} ({RoutingKey})",
            messageType.Name, routingKey ?? MessageRouting.For(messageType));

        return Task.CompletedTask;
    }
}
