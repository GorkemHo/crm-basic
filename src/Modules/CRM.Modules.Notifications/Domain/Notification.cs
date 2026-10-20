using CRM.BuildingBlocks.Domain;

namespace CRM.Modules.Notifications.Domain;

/// <summary>
/// Something a user should know about, shown in the app. The text is not stored: the UI builds it from
/// <see cref="Type"/>, the actor's name and <see cref="Subject"/> in the reader's language.
/// </summary>
internal sealed class Notification : Entity
{
    /// <summary>Recipient (Identity user).</summary>
    public int UserId { get; set; }

    /// <summary>One of <see cref="NotificationTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Who caused it; null for the system.</summary>
    public int? ActorId { get; set; }

    /// <summary>Title of the record it is about (task, deal...), as it was then.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>App route to open, e.g. <c>/deals/12</c>.</summary>
    public string? Link { get; set; }

    /// <summary>Integration event it was created from; with <see cref="UserId"/> makes redelivery harmless.</summary>
    public Guid SourceEventId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>Creates a notification for <paramref name="userId"/> unless it is the actor's own action.</summary>
    public static Notification? For(int? userId, int? actorId, string type, string subject, string? link, Guid sourceEventId, DateTime now) =>
        userId is { } recipient && recipient != actorId
            ? new Notification
            {
                UserId = recipient,
                ActorId = actorId,
                Type = type,
                Subject = subject,
                Link = link,
                SourceEventId = sourceEventId,
                CreatedAt = now
            }
            : null;
}

internal static class NotificationTypes
{
    public const string TaskAssigned = "task.assigned";
    public const string TaskCompleted = "task.completed";
    public const string DealWon = "deal.won";
    public const string DealLost = "deal.lost";
}
