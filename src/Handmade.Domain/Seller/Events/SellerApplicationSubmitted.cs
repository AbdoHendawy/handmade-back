using Handmade.Domain.Common;

namespace Handmade.Domain.Seller.Events;

public sealed class SellerApplicationSubmitted : IDomainEvent
{
    public SellerApplicationSubmitted(Guid applicationId, Guid userId)
    {
        ApplicationId = applicationId;
        UserId = userId;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid ApplicationId { get; }

    public Guid UserId { get; }

    public DateTimeOffset OccurredAt { get; }
}
