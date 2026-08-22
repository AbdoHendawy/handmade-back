using Handmade.Application.Notifications.DTOs;

namespace Handmade.Application.Notifications.Services;

public interface INotificationPublisher
{
    Task<NotificationResponse> PublishToUserAsync(
        CreateUserNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task PublishToRoleAsync(
        string roleName,
        string type,
        string title,
        string body,
        string idempotencyPrefix,
        string? dataJson = null,
        CancellationToken cancellationToken = default);
}
