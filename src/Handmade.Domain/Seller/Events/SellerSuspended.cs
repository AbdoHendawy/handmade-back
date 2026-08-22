using Handmade.Domain.Common;

namespace Handmade.Domain.Seller.Events;

public sealed class SellerSuspended : IDomainEvent
{
    public SellerSuspended(Guid sellerId, Guid userId, Guid suspendedBy, DateTimeOffset suspendedAt)
    {
        SellerId = sellerId;
        UserId = userId;
        SuspendedBy = suspendedBy;
        OccurredAt = suspendedAt;
    }

    public Guid SellerId { get; }

    public Guid UserId { get; }

    public Guid SuspendedBy { get; }

    public DateTimeOffset OccurredAt { get; }
}
