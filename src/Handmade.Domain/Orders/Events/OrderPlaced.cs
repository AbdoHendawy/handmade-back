using Handmade.Domain.Common;

namespace Handmade.Domain.Orders.Events;

public sealed class OrderPlaced : IDomainEvent
{
    public OrderPlaced(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
    {
        OrderId = orderId;
        OrderGroupId = orderGroupId;
        SellerId = sellerId;
        CustomerId = customerId;
        OccurredAt = occurredAt;
    }

    public Guid OrderId { get; }

    public Guid OrderGroupId { get; }

    public Guid SellerId { get; }

    public Guid CustomerId { get; }

    public DateTimeOffset OccurredAt { get; }
}
