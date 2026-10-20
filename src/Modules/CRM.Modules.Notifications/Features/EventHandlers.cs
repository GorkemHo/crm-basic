using CRM.BuildingBlocks.Messaging;
using CRM.Modules.Notifications.Domain;
using CRM.Modules.Notifications.Infrastructure.Persistence;
using CRM.Modules.Sales.Contracts;
using CRM.Modules.Tasks.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Notifications.Features;

/// <summary>
/// Turns other modules' events into notifications. Nobody is notified about their own action, and an event
/// delivered twice (the outbox is at-least-once) is recognised by its id.
/// </summary>
internal sealed class NotificationEventHandlers :
    IIntegrationEventHandler<TaskAssigned>,
    IIntegrationEventHandler<TaskCompleted>,
    IIntegrationEventHandler<DealWon>,
    IIntegrationEventHandler<DealLost>
{
    private readonly NotificationsDbContext _db;
    private readonly TimeProvider _clock;

    public NotificationEventHandlers(NotificationsDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>The assignee hears about work given to them.</summary>
    public Task HandleAsync(TaskAssigned e, CancellationToken cancellationToken) =>
        AddAsync(e, Create(e.AssigneeId, e.AssignedBy, NotificationTypes.TaskAssigned, e.Title, "/tasks"), cancellationToken);

    /// <summary>Whoever created the task hears that someone else finished it.</summary>
    public Task HandleAsync(TaskCompleted e, CancellationToken cancellationToken) =>
        AddAsync(e, Create(e.CreatedBy, e.CompletedBy, NotificationTypes.TaskCompleted, e.Title, "/tasks"), cancellationToken);

    /// <summary>Whoever added the deal hears that someone else won it.</summary>
    public Task HandleAsync(DealWon e, CancellationToken cancellationToken) =>
        AddAsync(e, Create(e.CreatedBy, e.ClosedBy, NotificationTypes.DealWon, e.Title, $"/deals/{e.DealId}"), cancellationToken);

    /// <summary>Whoever added the deal hears that someone else lost it.</summary>
    public Task HandleAsync(DealLost e, CancellationToken cancellationToken) =>
        AddAsync(e, Create(e.CreatedBy, e.ClosedBy, NotificationTypes.DealLost, e.Title, $"/deals/{e.DealId}"), cancellationToken);

    private Func<Guid, Notification?> Create(int? userId, int? actorId, string type, string subject, string link) =>
        eventId => Notification.For(userId, actorId, type, subject, link, eventId, _clock.GetUtcNow().UtcDateTime);

    private async Task AddAsync(IIntegrationEvent e, Func<Guid, Notification?> create, CancellationToken cancellationToken)
    {
        if(create(e.EventId) is not { } notification)
        {
            return;
        }

        if(await _db.Notifications.AnyAsync(x => x.SourceEventId == e.EventId && x.UserId == notification.UserId, cancellationToken))
        {
            return;
        }

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
