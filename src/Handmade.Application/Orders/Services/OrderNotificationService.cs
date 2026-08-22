using Handmade.Application.Notifications;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Orders.Services;

public interface IOrderNotificationService
{
    Task NotifyPlacedAsync(
        OrderGroup group,
        IReadOnlyList<Order> orders,
        IReadOnlyDictionary<Guid, Guid> sellerUserIds,
        CancellationToken cancellationToken = default);
}

public sealed class OrderNotificationService : IOrderNotificationService
{
    private readonly INotificationPublisher _publisher;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(INotificationPublisher publisher, ILogger<OrderNotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task NotifyPlacedAsync(
        OrderGroup group,
        IReadOnlyList<Order> orders,
        IReadOnlyDictionary<Guid, Guid> sellerUserIds,
        CancellationToken cancellationToken = default)
    {
        await TryPublishAsync(
            group.CustomerId,
            NotificationTypes.OrderPlaced,
            "Your Order Was Placed",
            "We received your order and sent it to the sellers.",
            $"{NotificationTypes.OrderPlaced}:{group.Id:D}",
            NotificationDataJson.Serialize(new { orderGroupId = group.Id, number = group.Number }),
            cancellationToken);

        foreach (Order order in orders)
        {
            if (!sellerUserIds.TryGetValue(order.SellerId, out Guid sellerUserId))
            {
                continue;
            }

            await TryPublishAsync(
                sellerUserId,
                NotificationTypes.OrderReceived,
                "You Received a New Order",
                "A customer placed an order with your shop.",
                $"{NotificationTypes.OrderReceived}:{order.Id:D}",
                NotificationDataJson.Serialize(new { orderId = order.Id, orderGroupId = group.Id, number = order.Number }),
                cancellationToken);
        }
    }

    private async Task TryPublishAsync(
        Guid userId,
        string type,
        string title,
        string body,
        string idempotencyKey,
        string dataJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishToUserAsync(
                new CreateUserNotificationRequest(userId, type, title, body, idempotencyKey, dataJson),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist {NotificationType} for user {UserId}. Business state is unchanged.",
                type,
                userId);
        }
    }
}
