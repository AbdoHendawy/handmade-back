using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
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
    private readonly INotificationPublisher _publisher;
    private readonly IValidator<CreateInboxNotificationRequest> _createValidator;
    private readonly IValidator<UpdateNotificationRequest> _updateValidator;

    public NotificationInboxService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        INotificationPublisher publisher,
        IValidator<CreateInboxNotificationRequest> createValidator,
        IValidator<UpdateNotificationRequest> updateValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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

    public async Task<NotificationResponse> CreateMineAsync(
        CreateInboxNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createValidator], cancellationToken);

        string idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"inbox:{userId:D}:{Guid.CreateVersion7():D}"
            : request.IdempotencyKey.Trim();

        return await _publisher.PublishToUserAsync(
            new CreateUserNotificationRequest(
                userId,
                request.Type,
                request.Title,
                request.Body,
                idempotencyKey,
                request.DataJson),
            cancellationToken);
    }

    public async Task<NotificationResponse> UpdateMineAsync(
        Guid notificationId,
        UpdateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);
        Notification notification = await LoadOwnedAsync(notificationId, cancellationToken);

        notification.UpdateContent(request.Title, request.Body, request.DataJson);
        if (request.IsRead)
        {
            notification.MarkRead(_clock.UtcNow);
        }
        else
        {
            notification.MarkUnread();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NotificationMapping.ToResponse(notification);
    }

    public async Task DeleteMineAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        Notification notification = await LoadOwnedAsync(notificationId, cancellationToken);
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllMineAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        List<Notification> items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.Notifications.RemoveRange(items);
        await _db.SaveChangesAsync(cancellationToken);
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
        List<Notification> unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (Notification notification in unread)
        {
            notification.MarkRead(now);
        }

        await _db.SaveChangesAsync(cancellationToken);
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
