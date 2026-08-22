namespace Handmade.Application.Abstractions.Jobs;

public interface IBackgroundJobQueue
{
    void EnqueueNotificationDelivery(Guid notificationId);
}
