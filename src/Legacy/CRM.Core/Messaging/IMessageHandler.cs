namespace CRM.Core.Messaging;

/// <summary>
/// Handles messages of type <typeparamref name="T"/> received from the broker.
/// Resolved in a new DI scope per message. Throwing rejects the message (it is not requeued).
/// </summary>
public interface IMessageHandler<in T> where T : class
{
    Task HandleAsync(T message, CancellationToken cancellationToken);
}
