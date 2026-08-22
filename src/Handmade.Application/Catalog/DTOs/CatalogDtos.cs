namespace Handmade.Application.Catalog.DTOs;

public sealed record CreateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    Guid? ParentCategoryId);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    Guid? ParentCategoryId);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CategoryTreeResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    IReadOnlyList<CategoryTreeResponse> Children);

public sealed record CreateProductRequest(
    string Name,
    string Description,
    Guid CategoryId,
    decimal Price,
    string? Currency,
    string? Slug,
    int StockQuantity = 0);

public sealed record UpdateProductRequest(
    string Name,
    string Description,
    Guid CategoryId,
    decimal Price,
    string? Currency,
    string? Slug,
    int StockQuantity = 0);

public sealed record RejectProductRequest(string Reason);

public sealed record AddProductImageRequest(string StorageKey, string? Url, int? SortOrder, bool IsPrimary);

public sealed record ReorderProductImagesRequest(IReadOnlyList<Guid> ImageIds);

public sealed record CreateProductVariantRequest(string Name, string Sku, decimal Price, string? Currency, int StockQuantity = 0);

public sealed record UpdateProductVariantRequest(string Name, string Sku, decimal Price, string? Currency, int StockQuantity = 0);

public sealed record SetStockRequest(int StockQuantity);

public sealed record ProductImageResponse(
    Guid Id,
    string StorageKey,
    string Url,
    int SortOrder,
    bool IsPrimary);

public sealed record ProductVariantResponse(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity);

public sealed record CatalogCategorySummary(Guid Id, string Name, string Slug);

public sealed record CatalogSellerSummary(Guid Id, string Name);

public sealed record ProductResponse(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string Slug,
    string Description,
    string Status,
    decimal Price,
    string Currency,
    int StockQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy,
    string? RejectionReason,
    CatalogCategorySummary Category,
    CatalogSellerSummary Seller,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductVariantResponse> Variants);

public sealed record PublicProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    DateTimeOffset? PublishedAt,
    CatalogCategorySummary Category,
    CatalogSellerSummary Seller,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductVariantResponse> Variants);

public sealed record ProductPurchaseKey(Guid ProductId, Guid? VariantId);

public sealed record ProductPurchaseInfo(
    Guid ProductId,
    Guid? VariantId,
    bool Exists,
    bool VariantExists,
    bool HasVariants,
    bool IsPublished,
    bool IsSellerActive,
    bool IsPurchasable,
    string? UnavailabilityReason,
    string Name,
    string? VariantName,
    string? ImageUrl,
    decimal UnitPrice,
    string Currency,
    Guid SellerId,
    string SellerName,
    string? Sku,
    int AvailableStock);
