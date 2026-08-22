using Hangfire;
using Handmade.Application.Abstractions.Jobs;
using Handmade.Application.Notifications.Services;

namespace Handmade.Infrastructure.Jobs;

public sealed class HangfireBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireBackgroundJobQueue(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void EnqueueNotificationDelivery(Guid notificationId)
    {
        _jobs.Enqueue<INotificationDeliveryService>(
            delivery => delivery.DeliverAsync(notificationId, CancellationToken.None));
    }
}
