using Handmade.Application.Notifications.DTOs;
using Handmade.Domain.Notifications;

namespace Handmade.Application.Notifications.Services;

internal static class NotificationMapping
{
    public static NotificationResponse ToResponse(Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.UserId,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.DataJson,
            notification.IsRead,
            notification.ReadAt,
            notification.DeliveryStatus.ToString(),
            notification.CreatedAt);
    }
}
