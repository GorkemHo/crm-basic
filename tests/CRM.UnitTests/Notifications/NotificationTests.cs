using CRM.Modules.Notifications.Domain;

namespace CRM.UnitTests.Notifications;

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void For_AnotherPersonsAction_CreatesAnUnreadNotification()
    {
        var eventId = Guid.NewGuid();

        var notification = Notification.For(userId: 5, actorId: 7, NotificationTypes.TaskAssigned, "Call back", "/tasks", eventId, Now);

        Assert.NotNull(notification);
        Assert.Equal(5, notification.UserId);
        Assert.Equal(7, notification.ActorId);
        Assert.Equal(eventId, notification.SourceEventId);
        Assert.Equal(Now, notification.CreatedAt);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void For_SystemActions_NotifyToo()
    {
        Assert.NotNull(Notification.For(userId: 5, actorId: null, NotificationTypes.TaskAssigned, "x", null, Guid.NewGuid(), Now));
    }

    [Fact]
    public void For_YourOwnAction_OrNoRecipient_CreatesNothing()
    {
        Assert.Null(Notification.For(userId: 5, actorId: 5, NotificationTypes.DealWon, "x", null, Guid.NewGuid(), Now));
        Assert.Null(Notification.For(userId: null, actorId: 5, NotificationTypes.DealWon, "x", null, Guid.NewGuid(), Now));
    }
}
