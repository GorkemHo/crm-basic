using System.Text.Json;
using RabbitMQ.Client;

namespace CRM.BuildingBlocks.Messaging.RabbitMq;

/// <summary>
/// Publishes JSON messages as persistent messages to the configured topic exchange (singleton).
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqConnectionProvider _connectionProvider;

    // Channels are not safe for concurrent publishing, so one channel is shared behind a lock.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public RabbitMqMessagePublisher(RabbitMqConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task PublishAsync(object message, Type messageType, string? routingKey = null, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, messageType, JsonSerializerOptions.Web);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = (message as IIntegrationEvent)?.EventId.ToString() ?? Guid.NewGuid().ToString(),
            Type = messageType.Name,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if(_channel is not { IsOpen: true })
            {
                _channel = await _connectionProvider.CreateChannelAsync(cancellationToken);
            }

            await _channel.BasicPublishAsync(
                exchange: _connectionProvider.Options.ExchangeName,
                routingKey: routingKey ?? MessageRouting.For(messageType),
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if(_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _lock.Dispose();
    }
}
