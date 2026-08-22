using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Application.Orders.Services;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handmade.Application.Tests;

public sealed class OrderNotificationServiceTests
{
    [Fact]
    public async Task PublisherThrow_DoesNotPropagate()
    {
        OrderNotificationService service = new(new ThrowingPublisher(), NullLogger<OrderNotificationService>.Instance);
        OrderGroup group = OrderGroup.Create(
            Guid.CreateVersion7(),
            "Nour",
            "Hassan",
            "nour@example.com",
            OrderDeliverySnapshot.Create(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                null,
                "Cairo",
                "Cairo",
                null,
                null),
            "EGP",
            DateTimeOffset.UtcNow);

        await service.NotifyPlacedAsync(group, [], new Dictionary<Guid, Guid>());
    }

    private sealed class ThrowingPublisher : INotificationPublisher
    {
        public Task<NotificationResponse> PublishToUserAsync(
            CreateUserNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Notification store failed.");
        }

        public Task PublishToRoleAsync(
            string roleName,
            string type,
            string title,
            string body,
            string idempotencyPrefix,
            string? dataJson = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Notification store failed.");
        }
    }
}
