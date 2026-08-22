using Handmade.Domain.Common;

namespace Handmade.Domain.Seller.Events;

public sealed class SellerReactivated : IDomainEvent
{
    public SellerReactivated(Guid sellerId, Guid userId, DateTimeOffset reactivatedAt)
    {
        SellerId = sellerId;
        UserId = userId;
        OccurredAt = reactivatedAt;
    }

    public Guid SellerId { get; }

    public Guid UserId { get; }

    public DateTimeOffset OccurredAt { get; }
}
