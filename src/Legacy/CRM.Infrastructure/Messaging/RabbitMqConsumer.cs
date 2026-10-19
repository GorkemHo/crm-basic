using System.Text.Json;
using CRM.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CRM.Infrastructure.Messaging;

/// <summary>Queue binding for one consumer, registered by <c>AddMessageConsumer</c>.</summary>
public sealed record RabbitMqConsumerOptions<T>(string QueueName, string RoutingKey) where T : class;

/// <summary>
/// Background service that consumes <typeparamref name="T"/> messages from a durable queue bound to
/// the exchange, and dispatches each one to <see cref="IMessageHandler{T}"/> in its own DI scope.
/// Successful handling acks the message; failures nack it without requeue.
/// </summary>
public sealed class RabbitMqConsumer<T> : BackgroundService where T : class
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqConsumerOptions<T> _consumerOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumer<T>> _logger;
    private IChannel? _channel;

    public RabbitMqConsumer(
        RabbitMqConnectionProvider connectionProvider,
        RabbitMqConsumerOptions<T> consumerOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumer<T>> logger)
    {
        _connectionProvider = connectionProvider;
        _consumerOptions = consumerOptions;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep retrying until the broker is reachable; the app itself keeps running meanwhile.
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                return;
            }
            catch(Exception exception) when(!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Could not start consumer for {Queue}; retrying in {Delay}",
                    _consumerOptions.QueueName, RetryDelay);

                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var options = _connectionProvider.Options;

        _channel = await _connectionProvider.CreateChannelAsync(stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _consumerOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: _consumerOptions.QueueName,
            exchange: options.ExchangeName,
            routingKey: _consumerOptions.RoutingKey,
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, args) => HandleDeliveryAsync(args, stoppingToken);

        await _channel.BasicConsumeAsync(
            queue: _consumerOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Consuming {MessageType} from {Queue}", typeof(T).Name, _consumerOptions.QueueName);
    }

    private async Task HandleDeliveryAsync(BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        var channel = _channel!;

        try
        {
            var message = JsonSerializer.Deserialize<T>(args.Body.Span, JsonSerializerOptions.Web)
                ?? throw new JsonException("Message body was empty.");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();

            await handler.HandleAsync(message, stoppingToken);

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, stoppingToken);
        }
        catch(Exception exception)
        {
            _logger.LogError(exception, "Failed to handle {MessageType} {MessageId} from {Queue}",
                typeof(T).Name, args.BasicProperties.MessageId, _consumerOptions.QueueName);

            // Not requeued to avoid poison-message loops; configure a dead-letter exchange on the queue to keep them.
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if(_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }
}
