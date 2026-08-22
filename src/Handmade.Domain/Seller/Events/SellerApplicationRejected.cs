using Handmade.Domain.Common;

namespace Handmade.Domain.Seller.Events;

public sealed class SellerApplicationRejected : IDomainEvent
{
    public SellerApplicationRejected(Guid applicationId, Guid userId, Guid rejectedBy, DateTimeOffset rejectedAt)
    {
        ApplicationId = applicationId;
        UserId = userId;
        RejectedBy = rejectedBy;
        OccurredAt = rejectedAt;
    }

    public Guid ApplicationId { get; }

    public Guid UserId { get; }

    public Guid RejectedBy { get; }

    public DateTimeOffset OccurredAt { get; }
}
