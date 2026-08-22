using Handmade.Domain.Common;

namespace Handmade.Domain.Catalog.Events;

public sealed class CategoryCreated : IDomainEvent
{
    public CategoryCreated(Guid categoryId, string slug, DateTimeOffset occurredAt)
    {
        CategoryId = categoryId;
        Slug = slug;
        OccurredAt = occurredAt;
    }

    public Guid CategoryId { get; }

    public string Slug { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class CategoryActivated : IDomainEvent
{
    public CategoryActivated(Guid categoryId, DateTimeOffset occurredAt)
    {
        CategoryId = categoryId;
        OccurredAt = occurredAt;
    }

    public Guid CategoryId { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class CategoryDeactivated : IDomainEvent
{
    public CategoryDeactivated(Guid categoryId, DateTimeOffset occurredAt)
    {
        CategoryId = categoryId;
        OccurredAt = occurredAt;
    }

    public Guid CategoryId { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductCreated : IDomainEvent
{
    public ProductCreated(Guid productId, Guid sellerId, Guid categoryId, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        CategoryId = categoryId;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public Guid CategoryId { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductSubmitted : IDomainEvent
{
    public ProductSubmitted(Guid productId, Guid sellerId, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductApproved : IDomainEvent
{
    public ProductApproved(Guid productId, Guid sellerId, Guid approvedBy, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        ApprovedBy = approvedBy;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public Guid ApprovedBy { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductRejected : IDomainEvent
{
    public ProductRejected(Guid productId, Guid sellerId, Guid rejectedBy, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        RejectedBy = rejectedBy;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public Guid RejectedBy { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductArchived : IDomainEvent
{
    public ProductArchived(Guid productId, Guid sellerId, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public DateTimeOffset OccurredAt { get; }
}

public sealed class ProductRestored : IDomainEvent
{
    public ProductRestored(Guid productId, Guid sellerId, DateTimeOffset occurredAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        OccurredAt = occurredAt;
    }

    public Guid ProductId { get; }

    public Guid SellerId { get; }

    public DateTimeOffset OccurredAt { get; }
}
