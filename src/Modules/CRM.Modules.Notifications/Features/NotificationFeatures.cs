using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Identity.Contracts;
using CRM.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Notifications.Features;

/// <param name="ActorName">Who caused it; null for the system.</param>
internal sealed record NotificationDto(
    int Id, string Type, int? ActorId, string? ActorName, string Subject, string? Link, DateTime CreatedAt, DateTime? ReadAt);

// Every request works on the caller's own notifications only.

/// <summary>The caller's notifications, newest first.</summary>
internal sealed class GetMyNotifications : PaginationRequest, IQuery<PagedResponse<NotificationDto>>
{
    public bool? UnreadOnly { get; set; }
}

internal sealed class GetMyNotificationsHandler : IRequestHandler<GetMyNotifications, PagedResponse<NotificationDto>>
{
    private readonly NotificationsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IUserDirectory _users;

    public GetMyNotificationsHandler(NotificationsDbContext db, ICurrentUser currentUser, IUserDirectory users)
    {
        _db = db;
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<PagedResponse<NotificationDto>> Handle(GetMyNotifications request, CancellationToken cancellationToken)
    {
        var query = _db.Notifications.AsNoTracking().Where(x => x.UserId == _currentUser.Id);
        if(request.UnreadOnly == true) query = query.Where(x => x.ReadAt == null);

        // Newest first, always: client sorting isn't offered for notifications.
        request.SortBy = null;
        var page = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(request, x => new NotificationDto(x.Id, x.Type, x.ActorId, null, x.Subject, x.Link, x.CreatedAt, x.ReadAt),
                cancellationToken);

        var names = await _users.GetNamesAsync(page.Items.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value), cancellationToken);

        return new PagedResponse<NotificationDto>
        {
            Items = page.Items.Select(x => x with { ActorName = x.ActorId is { } id ? names.GetValueOrDefault(id) : null }).ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }
}

internal sealed record UnreadCount(int Count);

internal sealed record GetMyUnreadCount : IQuery<UnreadCount>;

internal sealed class GetMyUnreadCountHandler : IRequestHandler<GetMyUnreadCount, UnreadCount>
{
    private readonly NotificationsDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyUnreadCountHandler(NotificationsDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UnreadCount> Handle(GetMyUnreadCount request, CancellationToken cancellationToken) =>
        new(await _db.Notifications.CountAsync(x => x.UserId == _currentUser.Id && x.ReadAt == null, cancellationToken));
}

internal sealed record MarkNotificationRead(int Id) : ICommand;

/// <summary>Marks every unread notification of the caller as read.</summary>
internal sealed record MarkAllNotificationsRead : ICommand;

internal sealed class MarkReadHandlers :
    IRequestHandler<MarkNotificationRead, Unit>,
    IRequestHandler<MarkAllNotificationsRead, Unit>
{
    private readonly NotificationsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public MarkReadHandlers(NotificationsDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Unit> Handle(MarkNotificationRead request, CancellationToken cancellationToken)
    {
        // Someone else's notification is "not found", so ids can't be probed.
        var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == _currentUser.Id, cancellationToken)
            ?? throw new NotFoundException("Notification", request.Id);

        notification.ReadAt ??= _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    public async Task<Unit> Handle(MarkAllNotificationsRead request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        await _db.Notifications
            .Where(x => x.UserId == _currentUser.Id && x.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, now), cancellationToken);

        return Unit.Value;
    }
}
