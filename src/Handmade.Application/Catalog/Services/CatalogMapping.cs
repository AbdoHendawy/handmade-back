using Handmade.Application.Catalog.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Seller;

namespace Handmade.Application.Catalog.Services;

internal static class CatalogMapping
{
    public static CategoryResponse ToResponse(Category category)
    {
        return new CategoryResponse(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ParentCategoryId,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public static ProductImageResponse ToResponse(ProductImage image)
    {
        return new ProductImageResponse(image.Id, image.StorageKey, image.Url, image.SortOrder, image.IsPrimary);
    }

    public static ProductVariantResponse ToResponse(ProductVariant variant)
    {
        return new ProductVariantResponse(variant.Id, variant.Name, variant.Sku, variant.Price, variant.Currency);
    }

    public static ProductResponse ToResponse(
        Product product,
        Category category,
        SellerProfile seller,
        IReadOnlyCollection<ProductImage>? images = null,
        IReadOnlyCollection<ProductVariant>? variants = null)
    {
        IReadOnlyCollection<ProductImage> imageList = images ?? product.Images;
        IReadOnlyCollection<ProductVariant> variantList = variants ?? product.Variants;
        return new ProductResponse(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Slug,
            product.Description,
            product.Status.ToString(),
            product.Price,
            product.Currency,
            product.CreatedAt,
            product.UpdatedAt,
            product.PublishedAt,
            product.ReviewedAt,
            product.ReviewedBy,
            product.RejectionReason,
            new CatalogCategorySummary(category.Id, category.Name, category.Slug),
            new CatalogSellerSummary(seller.Id, seller.BusinessName),
            imageList.OrderBy(i => i.SortOrder).Select(ToResponse).ToList(),
            variantList.OrderBy(v => v.Name).Select(ToResponse).ToList());
    }

    public static PublicProductResponse ToPublic(
        Product product,
        Category category,
        SellerProfile seller,
        IReadOnlyCollection<ProductImage>? images = null,
        IReadOnlyCollection<ProductVariant>? variants = null)
    {
        IReadOnlyCollection<ProductImage> imageList = images ?? product.Images;
        IReadOnlyCollection<ProductVariant> variantList = variants ?? product.Variants;
        return new PublicProductResponse(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            product.Currency,
            product.PublishedAt,
            new CatalogCategorySummary(category.Id, category.Name, category.Slug),
            new CatalogSellerSummary(seller.Id, seller.BusinessName),
            imageList.OrderBy(i => i.SortOrder).Select(ToResponse).ToList(),
            variantList.OrderBy(v => v.Name).Select(ToResponse).ToList());
    }
}
