using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Catalog.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

public interface IProductPurchaseQuery
{
    Task<ProductPurchaseInfo> GetForPurchaseAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPurchaseInfo>> GetManyForCartAsync(
        IReadOnlyList<ProductPurchaseKey> keys,
        CancellationToken cancellationToken = default);
}

public sealed class ProductPurchaseQuery : IProductPurchaseQuery
{
    public const string ReasonNotFound = "not_found";
    public const string ReasonUnpublished = "unpublished";
    public const string ReasonSellerNotActive = "seller_not_active";
    public const string ReasonVariantRequired = "variant_required";
    public const string ReasonVariantMissing = "variant_missing";

    private readonly IApplicationDbContext _db;

    public ProductPurchaseQuery(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ProductPurchaseInfo> GetForPurchaseAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        ProductPurchaseInfo info = (await GetManyForCartAsync(
            [new ProductPurchaseKey(productId, variantId)],
            cancellationToken))[0];
        EnsurePurchasable(info);
        return info;
    }

    public async Task<IReadOnlyList<ProductPurchaseInfo>> GetManyForCartAsync(
        IReadOnlyList<ProductPurchaseKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        List<Guid> productIds = keys.Select(k => k.ProductId).Distinct().ToList();
        Dictionary<Guid, Product> products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        List<Guid> sellerIds = products.Values.Select(p => p.SellerId).Distinct().ToList();
        Dictionary<Guid, SellerProfile> sellers = sellerIds.Count == 0
            ? []
            : await _db.SellerProfiles
                .AsNoTracking()
                .Where(s => sellerIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

        Dictionary<Guid, List<ProductVariant>> variantsByProduct = productIds.Count == 0
            ? []
            : (await _db.ProductVariants
                .AsNoTracking()
                .Where(v => productIds.Contains(v.ProductId))
                .ToListAsync(cancellationToken))
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<Guid, string> primaryImages = productIds.Count == 0
            ? []
            : await _db.ProductImages
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductId) && i.IsPrimary)
                .ToDictionaryAsync(i => i.ProductId, i => i.Url, cancellationToken);

        return keys.Select(key => Map(
            key,
            products.GetValueOrDefault(key.ProductId),
            variantsByProduct.GetValueOrDefault(key.ProductId) ?? [],
            primaryImages.GetValueOrDefault(key.ProductId),
            products.TryGetValue(key.ProductId, out Product? product)
                ? sellers.GetValueOrDefault(product.SellerId)
                : null)).ToList();
    }

    public static void EnsurePurchasable(ProductPurchaseInfo info)
    {
        if (!info.Exists)
        {
            throw new NotFoundException("Product", info.ProductId) { Code = CatalogErrorCodes.ProductNotFound };
        }

        if (info.UnavailabilityReason == ReasonVariantRequired)
        {
            throw new DomainException("This product requires a variant.")
            {
                Code = CatalogErrorCodes.VariantRequired
            };
        }

        if (info.UnavailabilityReason == ReasonVariantMissing)
        {
            throw new NotFoundException("ProductVariant", info.VariantId ?? Guid.Empty)
            {
                Code = CatalogErrorCodes.VariantNotFound
            };
        }

        if (!info.IsPublished)
        {
            throw new DomainException("This product is not available for purchase.")
            {
                Code = CatalogErrorCodes.ProductNotPurchasable
            };
        }

        if (!info.IsSellerActive)
        {
            throw new DomainException("This seller is not accepting purchases.")
            {
                Code = CatalogErrorCodes.SellerNotActive
            };
        }
    }

    private static ProductPurchaseInfo Map(
        ProductPurchaseKey key,
        Product? product,
        IReadOnlyList<ProductVariant> variants,
        string? imageUrl,
        SellerProfile? seller)
    {
        if (product is null)
        {
            return Missing(key);
        }

        bool hasVariants = variants.Count > 0;
        ProductVariant? variant = key.VariantId is Guid variantId
            ? variants.FirstOrDefault(v => v.Id == variantId)
            : null;
        bool variantExists = variant is not null;
        string? reason = ResolveReason(product, seller, hasVariants, key.VariantId, variantExists);
        decimal unitPrice = variant?.Price ?? product.Price;
        string currency = variant?.Currency ?? product.Currency;

        return new ProductPurchaseInfo(
            key.ProductId,
            key.VariantId,
            Exists: true,
            VariantExists: variantExists,
            HasVariants: hasVariants,
            IsPublished: product.Status == ProductStatus.Published,
            IsSellerActive: seller?.IsActive == true,
            IsPurchasable: reason is null,
            UnavailabilityReason: reason,
            product.Name,
            variant?.Name,
            imageUrl,
            unitPrice,
            currency,
            product.SellerId,
            seller?.BusinessName ?? string.Empty);
    }

    private static string? ResolveReason(
        Product product,
        SellerProfile? seller,
        bool hasVariants,
        Guid? variantId,
        bool variantExists)
    {
        if (hasVariants && variantId is null)
        {
            return ReasonVariantRequired;
        }

        if (variantId is not null && !variantExists)
        {
            return ReasonVariantMissing;
        }

        if (product.Status != ProductStatus.Published)
        {
            return ReasonUnpublished;
        }

        if (seller is null || !seller.IsActive)
        {
            return ReasonSellerNotActive;
        }

        return null;
    }

    private static ProductPurchaseInfo Missing(ProductPurchaseKey key)
    {
        return new ProductPurchaseInfo(
            key.ProductId,
            key.VariantId,
            Exists: false,
            VariantExists: false,
            HasVariants: false,
            IsPublished: false,
            IsSellerActive: false,
            IsPurchasable: false,
            UnavailabilityReason: ReasonNotFound,
            Name: string.Empty,
            VariantName: null,
            ImageUrl: null,
            UnitPrice: 0m,
            Currency: CatalogMoney.DefaultCurrency,
            SellerId: Guid.Empty,
            SellerName: string.Empty);
    }
}
