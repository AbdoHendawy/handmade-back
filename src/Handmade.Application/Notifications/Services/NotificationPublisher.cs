using Handmade.Application.Abstractions.Jobs;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Notifications.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Notifications.Services;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly IApplicationDbContext _db;
    private readonly IBackgroundJobQueue _jobs;

    public NotificationPublisher(IApplicationDbContext db, IBackgroundJobQueue jobs)
    {
        _db = db;
        _jobs = jobs;
    }

    public async Task<NotificationResponse> PublishToUserAsync(
        CreateUserNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        Notification? existing = await _db.Notifications
            .FirstOrDefaultAsync(n => n.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return NotificationMapping.ToResponse(existing);
        }

        Notification notification = Notification.CreateForUser(
            request.UserId,
            request.Type,
            request.Title,
            request.Body,
            request.IdempotencyKey,
            request.DataJson);

        _db.Notifications.Add(notification);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Notification? raced = await _db.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (raced is not null)
            {
                return NotificationMapping.ToResponse(raced);
            }

            throw;
        }

        _jobs.EnqueueNotificationDelivery(notification.Id);
        return NotificationMapping.ToResponse(notification);
    }

    public async Task PublishToRoleAsync(
        string roleName,
        string type,
        string title,
        string body,
        string idempotencyPrefix,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new DomainException("Role name is required.") { Code = "invalid_role" };
        }

        Role? role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role is null)
        {
            return;
        }

        List<Guid> userIds = await _db.UserRoles
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (Guid userId in userIds)
        {
            await PublishToUserAsync(
                new CreateUserNotificationRequest(
                    userId,
                    type,
                    title,
                    body,
                    $"{idempotencyPrefix}:{userId:D}",
                    dataJson),
                cancellationToken);
        }
    }
}
