namespace Handmade.Application.Notifications.Services;

public interface INotificationDeliveryService
{
    Task DeliverAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
