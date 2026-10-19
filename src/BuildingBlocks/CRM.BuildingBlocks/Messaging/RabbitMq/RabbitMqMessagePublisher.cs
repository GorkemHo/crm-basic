using System.Text.Json;
using CRM.BuildingBlocks.Messaging;
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

    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Type = typeof(T).Name,
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
                routingKey: routingKey ?? MessageRouting.For<T>(),
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
