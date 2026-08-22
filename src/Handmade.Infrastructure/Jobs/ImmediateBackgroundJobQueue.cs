using Handmade.Application.Abstractions.Jobs;
using Handmade.Application.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Infrastructure.Jobs;

public sealed class ImmediateBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ImmediateBackgroundJobQueue(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void EnqueueNotificationDelivery(Guid notificationId)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        INotificationDeliveryService delivery = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();
        delivery.DeliverAsync(notificationId).GetAwaiter().GetResult();
    }
}
