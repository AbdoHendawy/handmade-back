using Handmade.Domain.Common;

namespace Handmade.Domain.Orders.Events;

public sealed class OrderGroupPlaced : IDomainEvent
{
    public OrderGroupPlaced(Guid orderGroupId, Guid customerId, DateTimeOffset occurredAt)
    {
        OrderGroupId = orderGroupId;
        CustomerId = customerId;
        OccurredAt = occurredAt;
    }

    public Guid OrderGroupId { get; }

    public Guid CustomerId { get; }

    public DateTimeOffset OccurredAt { get; }
}
