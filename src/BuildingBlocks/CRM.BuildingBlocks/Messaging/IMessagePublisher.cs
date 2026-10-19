namespace CRM.BuildingBlocks.Messaging;

/// <summary>
/// Publishes integration messages to the message broker.
/// When messaging is disabled a no-op implementation is registered, so callers never need to check.
/// </summary>
public interface IMessagePublisher
{
    /// <param name="message">Serialized as JSON.</param>
    /// <param name="routingKey">Defaults to <see cref="MessageRouting.For{T}"/>.</param>
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class;
}
