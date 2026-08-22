using Handmade.Domain.Common;

namespace Handmade.Domain.Seller.Events;

public sealed class SellerApplicationApproved : IDomainEvent
{
    public SellerApplicationApproved(
        Guid applicationId,
        Guid sellerId,
        Guid userId,
        Guid approvedBy,
        DateTimeOffset approvedAt)
    {
        ApplicationId = applicationId;
        SellerId = sellerId;
        UserId = userId;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        OccurredAt = approvedAt;
    }

    public Guid ApplicationId { get; }

    public Guid SellerId { get; }

    public Guid UserId { get; }

    public Guid ApprovedBy { get; }

    public DateTimeOffset ApprovedAt { get; }

    public DateTimeOffset OccurredAt { get; }
}
