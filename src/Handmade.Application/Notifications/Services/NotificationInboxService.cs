using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Notifications.Services;

public sealed class NotificationInboxService : INotificationInboxService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public NotificationInboxService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<NotificationResponse>> ListMineAsync(
        bool unreadOnly,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();

        IQueryable<Notification> query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        int total = await query.CountAsync(cancellationToken);
        List<Notification> items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationResponse>(
            items.Select(NotificationMapping.ToResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<NotificationResponse> GetMineAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        Notification notification = await LoadOwnedAsync(notificationId, cancellationToken);
        return NotificationMapping.ToResponse(notification);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        int count = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
        return new UnreadCountResponse(count);
    }

    public async Task<NotificationResponse> MarkReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        Notification notification = await LoadOwnedAsync(notificationId, cancellationToken);
        notification.MarkRead(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return NotificationMapping.ToResponse(notification);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        DateTimeOffset now = _clock.UtcNow;
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now)
                    .SetProperty(n => n.UpdatedAt, now),
                cancellationToken);
    }

    private async Task<Notification> LoadOwnedAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        Guid userId = RequireUserId();
        Notification notification = await _db.Notifications
                                        .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
                                    ?? throw new NotFoundException("Notification", notificationId);

        if (notification.UserId != userId)
        {
            throw new NotFoundException("Notification", notificationId);
        }

        return notification;
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }
}
