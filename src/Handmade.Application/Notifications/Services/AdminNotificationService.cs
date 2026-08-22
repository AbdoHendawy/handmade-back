using FluentValidation;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Notifications.Services;

public sealed class AdminNotificationService : IAdminNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationPublisher _publisher;
    private readonly IValidator<AdminCreateNotificationRequest> _createValidator;
    private readonly IValidator<UpdateNotificationRequest> _updateValidator;

    public AdminNotificationService(
        IApplicationDbContext db,
        IClock clock,
        INotificationPublisher publisher,
        IValidator<AdminCreateNotificationRequest> createValidator,
        IValidator<UpdateNotificationRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _publisher = publisher;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<NotificationResponse>> ListAsync(
        Guid? userId,
        bool unreadOnly,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Notification> query = _db.Notifications.AsNoTracking();
        if (userId.HasValue)
        {
            query = query.Where(n => n.UserId == userId.Value);
        }

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

    public async Task<NotificationResponse> GetAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        Notification notification = await LoadAsync(notificationId, cancellationToken);
        return NotificationMapping.ToResponse(notification);
    }

    public async Task<NotificationResponse> CreateForUserAsync(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createValidator], cancellationToken);
        Guid userId = request.UserId
                      ?? throw new DomainException("User id is required.") { Code = NotificationErrorCodes.InvalidUser };

        bool userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException("User", userId);
        }

        string idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"admin:{userId:D}:{Guid.CreateVersion7():D}"
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

    public async Task<AdminCreateNotificationResponse> CreateForRoleAsync(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createValidator], cancellationToken);
        string roleName = request.RoleName?.Trim()
                          ?? throw new DomainException("Role name is required.") { Code = "invalid_role" };

        Role role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
                    ?? throw new NotFoundException("Role", roleName);

        string prefix = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"admin-role:{role.Name}:{Guid.CreateVersion7():D}"
            : request.IdempotencyKey.Trim();

        int recipients = await _db.UserRoles.CountAsync(ur => ur.RoleId == role.Id, cancellationToken);
        await _publisher.PublishToRoleAsync(
            role.Name,
            request.Type,
            request.Title,
            request.Body,
            prefix,
            request.DataJson,
            cancellationToken);

        return new AdminCreateNotificationResponse(recipients);
    }

    public async Task<NotificationResponse> UpdateAsync(
        Guid notificationId,
        UpdateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);
        Notification notification = await LoadAsync(notificationId, cancellationToken);

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

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        Notification notification = await LoadAsync(notificationId, cancellationToken);
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Notification> LoadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        return await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
               ?? throw new NotFoundException("Notification", notificationId);
    }
}
