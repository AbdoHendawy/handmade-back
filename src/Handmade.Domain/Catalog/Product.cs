using Handmade.Domain.Catalog.Events;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

public sealed class Product : AggregateRoot, IAuditable
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 200;
    public const int DescriptionMinLength = 20;
    public const int DescriptionMaxLength = 4000;
    public const int SlugMaxLength = 220;
    public const int RejectionReasonMinLength = 10;
    public const int RejectionReasonMaxLength = 1000;

    private readonly List<ProductImage> _images = [];
    private readonly List<ProductVariant> _variants = [];

    private Product()
    {
    }

    private Product(
        Guid id,
        Guid sellerId,
        Guid categoryId,
        string name,
        string slug,
        string description,
        decimal price,
        string currency)
        : base(id)
    {
        SellerId = sellerId;
        CategoryId = categoryId;
        Name = name;
        Slug = slug;
        Description = description;
        Price = price;
        Currency = currency;
        Status = ProductStatus.Draft;
    }

    public Guid SellerId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ProductStatus Status { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = CatalogMoney.DefaultCurrency;

    public int StockQuantity { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    public bool IsPublic => Status == ProductStatus.Published;

    public bool CanDelete => Status is ProductStatus.Draft or ProductStatus.Rejected;

    public static Product Create(
        Guid sellerId,
        Guid categoryId,
        string name,
        string slug,
        string description,
        decimal price,
        string currency,
        DateTimeOffset now)
    {
        if (sellerId == Guid.Empty)
        {
            throw new DomainException("Seller is required.") { Code = CatalogErrorCodes.ProductNotOwned };
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Category is required.") { Code = CatalogErrorCodes.CategoryNotFound };
        }

        Product product = new(
            CreateId(),
            sellerId,
            categoryId,
            RequireName(name),
            CatalogSlug.Require(slug),
            RequireDescription(description, requireComplete: false),
            CatalogMoney.RequireAmount(price),
            CatalogMoney.RequireCurrency(currency));

        product.Raise(new ProductCreated(product.Id, sellerId, categoryId, now));
        return product;
    }

    public void UpdateDetails(string name, string description, Guid categoryId, decimal price, string currency)
    {
        EnsureEditable();
        Name = RequireName(name);
        Description = RequireDescription(description, requireComplete: false);
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Category is required.") { Code = CatalogErrorCodes.CategoryNotFound };
        }

        CategoryId = categoryId;
        Price = CatalogMoney.RequireAmount(price);
        Currency = CatalogMoney.RequireCurrency(currency);
    }

    public void ReplaceSlug(string slug)
    {
        EnsureEditable();
        Slug = CatalogSlug.Require(slug);
    }

    public void Submit(DateTimeOffset now) => Submit(now, _images.Count > 0);

    public void Submit(DateTimeOffset now, bool hasAtLeastOneImage)
    {
        if (Status is not (ProductStatus.Draft or ProductStatus.Rejected))
        {
            throw InvalidTransition("Only draft or rejected products can be submitted.");
        }

        RequireDescription(Description, requireComplete: true);
        if (!hasAtLeastOneImage)
        {
            throw new DomainException("A product needs at least one image before submit.")
            {
                Code = CatalogErrorCodes.ProductIncomplete
            };
        }

        Status = ProductStatus.PendingReview;
        RejectionReason = null;
        Raise(new ProductSubmitted(Id, SellerId, now));
    }

    public void CancelSubmission()
    {
        if (Status != ProductStatus.PendingReview)
        {
            throw InvalidTransition("Only products pending review can cancel submission.");
        }

        Status = ProductStatus.Draft;
    }

    public void Approve(Guid approvedBy, DateTimeOffset now)
    {
        if (Status != ProductStatus.PendingReview)
        {
            throw InvalidTransition("Only products pending review can be approved.");
        }

        if (approvedBy == Guid.Empty)
        {
            throw new DomainException("Reviewer is required.") { Code = "invalid_reviewer" };
        }

        Status = ProductStatus.Published;
        PublishedAt = now;
        ReviewedAt = now;
        ReviewedBy = approvedBy;
        RejectionReason = null;
        Raise(new ProductApproved(Id, SellerId, approvedBy, now));
    }

    public void Reject(Guid rejectedBy, string reason, DateTimeOffset now)
    {
        if (Status != ProductStatus.PendingReview)
        {
            throw InvalidTransition("Only products pending review can be rejected.");
        }

        if (rejectedBy == Guid.Empty)
        {
            throw new DomainException("Reviewer is required.") { Code = "invalid_reviewer" };
        }

        string trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length < RejectionReasonMinLength || trimmed.Length > RejectionReasonMaxLength)
        {
            throw new DomainException("Rejection reason must be between 10 and 1000 characters.")
            {
                Code = CatalogErrorCodes.RejectionReasonRequired
            };
        }

        Status = ProductStatus.Rejected;
        ReviewedAt = now;
        ReviewedBy = rejectedBy;
        RejectionReason = trimmed;
        Raise(new ProductRejected(Id, SellerId, rejectedBy, now));
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status != ProductStatus.Published)
        {
            throw InvalidTransition("Only published products can be archived.");
        }

        Status = ProductStatus.Archived;
        Raise(new ProductArchived(Id, SellerId, now));
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status != ProductStatus.Archived)
        {
            throw InvalidTransition("Only archived products can be restored.");
        }

        Status = ProductStatus.Draft;
        Raise(new ProductRestored(Id, SellerId, now));
    }

    public void AssertEditable() => EnsureEditable();

    public ProductImage AddImage(string storageKey, string? url, int? sortOrder, bool isPrimary)
    {
        EnsureEditable();
        string key = ProductImage.RequireStorageKey(storageKey);
        string resolvedUrl = ProductImage.RequireUrl(url, key);
        int order = sortOrder ?? (_images.Count == 0 ? 1 : _images.Max(i => i.SortOrder) + 1);
        if (order < 1)
        {
            throw new DomainException("Sort order must be at least 1.") { Code = CatalogErrorCodes.InvalidSortOrder };
        }

        bool primary = isPrimary || _images.Count == 0;
        if (primary)
        {
            foreach (ProductImage image in _images)
            {
                image.ClearPrimary();
            }
        }

        ProductImage created = new(CreateId(), Id, key, resolvedUrl, order, primary);
        _images.Add(created);
        return created;
    }

    public void RemoveImage(Guid imageId)
    {
        EnsureEditable();
        ProductImage image = _images.FirstOrDefault(i => i.Id == imageId)
                             ?? throw new NotFoundException("ProductImage", imageId);
        _images.Remove(image);
        if (image.IsPrimary && _images.Count > 0)
        {
            _images.OrderBy(i => i.SortOrder).First().MarkPrimary();
        }
    }

    public void SetPrimaryImage(Guid imageId)
    {
        EnsureEditable();
        ProductImage target = _images.FirstOrDefault(i => i.Id == imageId)
                              ?? throw new NotFoundException("ProductImage", imageId);
        foreach (ProductImage image in _images)
        {
            if (image.Id == target.Id)
            {
                image.MarkPrimary();
            }
            else
            {
                image.ClearPrimary();
            }
        }
    }

    public void ReorderImages(IReadOnlyList<Guid> orderedIds)
    {
        EnsureEditable();
        if (orderedIds.Count != _images.Count || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            throw new DomainException("Reorder must include every image exactly once.")
            {
                Code = CatalogErrorCodes.InvalidImageReorder
            };
        }

        for (int i = 0; i < orderedIds.Count; i++)
        {
            ProductImage image = _images.FirstOrDefault(img => img.Id == orderedIds[i])
                                 ?? throw new NotFoundException("ProductImage", orderedIds[i]);
            image.SetSortOrder(i + 1);
        }
    }

    public ProductVariant AddVariant(string name, string sku, decimal price, string? currency)
    {
        EnsureEditable();
        string normalizedSku = ProductVariant.RequireSku(sku);
        if (_variants.Any(v => v.Sku == normalizedSku))
        {
            throw new ConflictException("A variant with this SKU already exists on the product.")
            {
                Code = CatalogErrorCodes.DuplicateSku
            };
        }

        ProductVariant variant = new(
            CreateId(),
            Id,
            ProductVariant.RequireName(name),
            normalizedSku,
            CatalogMoney.RequireAmount(price),
            CatalogMoney.RequireCurrency(currency ?? Currency));
        _variants.Add(variant);
        return variant;
    }

    public ProductVariant UpdateVariant(Guid variantId, string name, string sku, decimal price, string? currency)
    {
        EnsureEditable();
        ProductVariant variant = _variants.FirstOrDefault(v => v.Id == variantId)
                                 ?? throw new NotFoundException("ProductVariant", variantId);
        string normalizedSku = ProductVariant.RequireSku(sku);
        if (_variants.Any(v => v.Id != variantId && v.Sku == normalizedSku))
        {
            throw new ConflictException("A variant with this SKU already exists on the product.")
            {
                Code = CatalogErrorCodes.DuplicateSku
            };
        }

        variant.Update(name, normalizedSku, price, currency ?? Currency);
        return variant;
    }

    public void RemoveVariant(Guid variantId)
    {
        EnsureEditable();
        ProductVariant variant = _variants.FirstOrDefault(v => v.Id == variantId)
                                 ?? throw new NotFoundException("ProductVariant", variantId);
        _variants.Remove(variant);
    }

    public void SetStock(int quantity)
    {
        EnsureEditable();
        StockQuantity = RequireNonNegativeStock(quantity);
    }

    public void DecrementStock(int quantity)
    {
        StockQuantity = ApplyDecrement(StockQuantity, quantity);
    }

    public void IncrementStock(int quantity)
    {
        StockQuantity = ApplyIncrement(StockQuantity, quantity);
    }

    public void EnsureOwnedBy(Guid sellerId)
    {
        if (SellerId != sellerId)
        {
            throw new NotFoundException("Product", Id);
        }
    }

    internal static int RequireNonNegativeStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("Stock quantity cannot be negative.")
            {
                Code = CatalogErrorCodes.InvalidStockQuantity
            };
        }

        return quantity;
    }

    internal static int ApplyDecrement(int current, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity to decrement must be greater than zero.")
            {
                Code = CatalogErrorCodes.InvalidStockQuantity
            };
        }

        if (quantity > current)
        {
            throw new DomainException("Not enough stock for this product.")
            {
                Code = CatalogErrorCodes.InsufficientStock
            };
        }

        return current - quantity;
    }

    internal static int ApplyIncrement(int current, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity to increment must be greater than zero.")
            {
                Code = CatalogErrorCodes.InvalidStockQuantity
            };
        }

        return current + quantity;
    }

    private void EnsureEditable()
    {
        if (Status is ProductStatus.PendingReview)
        {
            throw new ConflictException("Cancel the submission before editing a product pending review.")
            {
                Code = CatalogErrorCodes.ProductNotEditable
            };
        }

        if (Status is ProductStatus.Archived)
        {
            throw new ConflictException("Restore an archived product before editing.")
            {
                Code = CatalogErrorCodes.ProductNotEditable
            };
        }
    }

    private static ConflictException InvalidTransition(string message)
    {
        return new ConflictException(message) { Code = CatalogErrorCodes.InvalidStateTransition };
    }

    public static string RequireName(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length < NameMinLength || trimmed.Length > NameMaxLength)
        {
            throw new DomainException("Product name must be between 2 and 200 characters.")
            {
                Code = CatalogErrorCodes.InvalidName
            };
        }

        return trimmed;
    }

    public static string RequireDescription(string description, bool requireComplete)
    {
        string trimmed = description?.Trim() ?? string.Empty;
        int min = requireComplete ? DescriptionMinLength : 1;
        if (trimmed.Length < min || trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainException(
                requireComplete
                    ? "Product description must be at least 20 characters before submit."
                    : "Product description is required.")
            {
                Code = CatalogErrorCodes.InvalidDescription
            };
        }

        return trimmed;
    }
}
