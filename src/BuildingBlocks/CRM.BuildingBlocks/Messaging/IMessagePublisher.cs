namespace CRM.BuildingBlocks.Messaging;

/// <summary>
/// Publishes messages to the external message broker.
/// When messaging is disabled a no-op implementation is registered, so callers never need to check.
/// Module code should not call this directly: add integration events to the outbox instead
/// (<c>ModuleDbContext.AddIntegrationEvent</c>), which publishes here after the transaction commits.
/// </summary>
public interface IMessagePublisher
{
    /// <param name="message">Serialized as JSON using <paramref name="messageType"/>.</param>
    /// <param name="routingKey">Defaults to <see cref="MessageRouting.For(Type)"/>.</param>
    Task PublishAsync(object message, Type messageType, string? routingKey = null, CancellationToken cancellationToken = default);
}

public static class MessagePublisherExtensions
{
    public static Task PublishAsync<T>(this IMessagePublisher publisher, T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class =>
        publisher.PublishAsync(message, typeof(T), routingKey, cancellationToken);
}
