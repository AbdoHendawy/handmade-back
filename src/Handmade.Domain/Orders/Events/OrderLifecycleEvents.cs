using Handmade.Domain.Common;

namespace Handmade.Domain.Orders.Events;

public sealed class OrderConfirmed : IDomainEvent
{
    public OrderConfirmed(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
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

public sealed class OrderPreparing : IDomainEvent
{
    public OrderPreparing(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
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

public sealed class OrderShipped : IDomainEvent
{
    public OrderShipped(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
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

public sealed class OrderDelivered : IDomainEvent
{
    public OrderDelivered(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
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

public sealed class OrderCancelled : IDomainEvent
{
    public OrderCancelled(Guid orderId, Guid orderGroupId, Guid sellerId, Guid customerId, DateTimeOffset occurredAt)
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
