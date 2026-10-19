namespace CRM.BuildingBlocks.Messaging;

/// <summary>
/// Something that happened in a module and other modules (or external systems) may react to.
/// Define events in the publishing module's <c>Contracts</c> namespace as immutable records.
/// They are stored in the outbox with the business change and delivered after commit.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime OccurredAt { get; }
}

/// <summary>Base record that fills <see cref="EventId"/> and <see cref="OccurredAt"/>.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// In-process subscriber to an integration event (usually in another module). Runs in its own DI scope,
/// at-least-once: make it idempotent.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
