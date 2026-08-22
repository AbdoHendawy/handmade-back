using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Application.Orders.Services;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handmade.Application.Tests;

public sealed class OrderNotificationServiceTests
{
    [Fact]
    public async Task NotifyPlaced_PublishesCustomerAndSeller_AndSkipsUnknownSeller()
    {
        RecordingPublisher publisher = new();
        OrderNotificationService service = new(publisher, NullLogger<OrderNotificationService>.Instance);
        OrderGroup group = CreateGroup();
        Order known = CreateOrder(group.Id, group.CustomerId);
        Order unknown = CreateOrder(group.Id, group.CustomerId);
        Guid sellerUserId = Guid.CreateVersion7();

        await service.NotifyPlacedAsync(
            group,
            [known, unknown],
            new Dictionary<Guid, Guid> { [known.SellerId] = sellerUserId });

        Assert.Collection(
            publisher.Requests,
            placed =>
            {
                Assert.Equal(group.CustomerId, placed.UserId);
                Assert.Equal(NotificationTypes.OrderPlaced, placed.Type);
                Assert.Equal($"{NotificationTypes.OrderPlaced}:{group.Id:D}", placed.IdempotencyKey);
            },
            received =>
            {
                Assert.Equal(sellerUserId, received.UserId);
                Assert.Equal(NotificationTypes.OrderReceived, received.Type);
                Assert.Equal($"{NotificationTypes.OrderReceived}:{known.Id:D}", received.IdempotencyKey);
                Assert.DoesNotContain(unknown.Id.ToString("D"), received.IdempotencyKey, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task PublisherThrow_DoesNotPropagate()
    {
        OrderNotificationService service = new(new ThrowingPublisher(), NullLogger<OrderNotificationService>.Instance);
        OrderGroup group = CreateGroup();
        Order order = CreateOrder(group.Id, group.CustomerId);

        await service.NotifyPlacedAsync(group, [order], new Dictionary<Guid, Guid> { [order.SellerId] = Guid.CreateVersion7() });
        await service.NotifyConfirmedAsync(order);
        await service.NotifyPreparingAsync(order);
        await service.NotifyShippedAsync(order);
        await service.NotifyDeliveredAsync(order);
        await service.NotifyCancelledAsync(order, order.CustomerId);
        await service.NotifyCancelledAsync(order, Guid.CreateVersion7());
    }

    [Fact]
    public async Task LifecycleNotifications_GoToExpectedRecipient_WithOrderIdempotency()
    {
        RecordingPublisher publisher = new();
        OrderNotificationService service = new(publisher, NullLogger<OrderNotificationService>.Instance);
        Order order = CreateOrder(Guid.CreateVersion7(), Guid.CreateVersion7());
        Guid sellerUserId = Guid.CreateVersion7();

        await service.NotifyConfirmedAsync(order);
        await service.NotifyPreparingAsync(order);
        await service.NotifyShippedAsync(order);
        await service.NotifyDeliveredAsync(order);
        await service.NotifyCancelledAsync(order, order.CustomerId);
        await service.NotifyCancelledAsync(order, sellerUserId);

        Assert.Collection(
            publisher.Requests,
            confirmed => AssertCustomerLifecycle(confirmed, order, NotificationTypes.OrderConfirmed),
            preparing => AssertCustomerLifecycle(preparing, order, NotificationTypes.OrderPreparing),
            shipped => AssertCustomerLifecycle(shipped, order, NotificationTypes.OrderShipped),
            delivered => AssertCustomerLifecycle(delivered, order, NotificationTypes.OrderDelivered),
            customerCancelled =>
            {
                AssertCustomerLifecycle(customerCancelled, order, NotificationTypes.OrderCancelled);
                Assert.Equal("Your Order Was Cancelled", customerCancelled.Title);
            },
            sellerCancelled =>
            {
                Assert.Equal(sellerUserId, sellerCancelled.UserId);
                Assert.Equal(NotificationTypes.OrderCancelled, sellerCancelled.Type);
                Assert.Equal($"{NotificationTypes.OrderCancelled}:{order.Id:D}", sellerCancelled.IdempotencyKey);
                Assert.Equal("An Order Was Cancelled", sellerCancelled.Title);
                Assert.Contains(order.Id.ToString("D"), sellerCancelled.DataJson, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static void AssertCustomerLifecycle(CreateUserNotificationRequest request, Order order, string type)
    {
        Assert.Equal(order.CustomerId, request.UserId);
        Assert.Equal(type, request.Type);
        Assert.Equal($"{type}:{order.Id:D}", request.IdempotencyKey);
        Assert.Contains(order.Id.ToString("D"), request.DataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(order.OrderGroupId.ToString("D"), request.DataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paid", request.Type, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refund", request.Type, StringComparison.OrdinalIgnoreCase);
    }

    private static OrderGroup CreateGroup()
    {
        return OrderGroup.Create(
            Guid.CreateVersion7(),
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "EGP",
            PaymentMethod.CashOnDelivery,
            DateTimeOffset.UtcNow);
    }

    private static Order CreateOrder(Guid orderGroupId, Guid customerId)
    {
        return Order.Create(
            orderGroupId,
            customerId,
            Guid.CreateVersion7(),
            "Atelier Nile",
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "EGP",
            DateTimeOffset.UtcNow);
    }

    private static OrderDeliverySnapshot SampleDelivery()
    {
        return OrderDeliverySnapshot.Create(
            "Nour Hassan",
            "+201001234567",
            "12 Nile Street",
            null,
            "Cairo",
            "Cairo",
            null,
            null);
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

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public List<CreateUserNotificationRequest> Requests { get; } = [];

        public Task<NotificationResponse> PublishToUserAsync(
            CreateUserNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new NotificationResponse(
                Guid.CreateVersion7(),
                request.UserId,
                request.Type,
                request.Title,
                request.Body,
                request.DataJson,
                false,
                null,
                "Pending",
                DateTimeOffset.UtcNow));
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
            return Task.CompletedTask;
        }
    }
}
