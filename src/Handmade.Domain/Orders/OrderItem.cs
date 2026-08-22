using Handmade.Domain.Catalog;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Orders;

public sealed class OrderItem : Entity, IAuditable
{
    public const int ProductNameMaxLength = 200;
    public const int VariantNameMaxLength = 120;
    public const int SkuMaxLength = 64;
    public const int ImageUrlMaxLength = 1000;

    private OrderItem()
    {
    }

    private OrderItem(
        Guid id,
        Guid orderId,
        Guid productId,
        Guid? variantId,
        Guid sellerId,
        string productNameSnapshot,
        string? variantNameSnapshot,
        string? skuSnapshot,
        string? imageUrlSnapshot,
        int quantity,
        decimal unitPrice,
        decimal lineTotal,
        string currency)
        : base(id)
    {
        OrderId = orderId;
        ProductId = productId;
        VariantId = variantId;
        SellerId = sellerId;
        ProductNameSnapshot = productNameSnapshot;
        VariantNameSnapshot = variantNameSnapshot;
        SkuSnapshot = skuSnapshot;
        ImageUrlSnapshot = imageUrlSnapshot;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
        Currency = currency;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? VariantId { get; private set; }

    public Guid SellerId { get; private set; }

    public string ProductNameSnapshot { get; private set; } = string.Empty;

    public string? VariantNameSnapshot { get; private set; }

    public string? SkuSnapshot { get; private set; }

    public string? ImageUrlSnapshot { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    public string Currency { get; private set; } = CatalogMoney.DefaultCurrency;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrderItem Create(
        Guid orderId,
        Guid productId,
        Guid? variantId,
        Guid sellerId,
        string productNameSnapshot,
        string? variantNameSnapshot,
        string? skuSnapshot,
        string? imageUrlSnapshot,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order is required.") { Code = OrderErrorCodes.OrderNotFound };
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Product is required.") { Code = OrderErrorCodes.LineNotPurchasable };
        }

        if (sellerId == Guid.Empty)
        {
            throw new DomainException("Seller is required.") { Code = OrderErrorCodes.SellerMismatch };
        }

        Guid? normalizedVariant = NormalizeVariantId(variantId);
        string productName = RequireText(productNameSnapshot, ProductNameMaxLength, "Product name snapshot is required.");
        string? variantName = OptionalText(variantNameSnapshot, VariantNameMaxLength, "Variant name snapshot is too long.");
        string? sku = OptionalText(skuSnapshot, SkuMaxLength, "SKU snapshot is too long.");
        string? imageUrl = OptionalText(imageUrlSnapshot, ImageUrlMaxLength, "Image URL snapshot is too long.");

        if (normalizedVariant is not null)
        {
            if (variantName is null)
            {
                throw new DomainException("Variant name snapshot is required for a variant line.")
                {
                    Code = "invalid_snapshot"
                };
            }

            if (sku is null)
            {
                throw new DomainException("SKU snapshot is required for a variant line.")
                {
                    Code = "invalid_snapshot"
                };
            }
        }
        else
        {
            if (variantName is not null)
            {
                throw new DomainException("Non-variant lines cannot include a variant name snapshot.")
                {
                    Code = "invalid_snapshot"
                };
            }

            if (sku is not null)
            {
                throw new DomainException("Non-variant lines cannot include a SKU snapshot.")
                {
                    Code = "invalid_snapshot"
                };
            }
        }

        int normalizedQuantity = RequireQuantity(quantity);
        decimal normalizedUnitPrice = CatalogMoney.RequireAmount(unitPrice);
        string normalizedCurrency = CatalogMoney.RequireCurrency(currency);
        decimal lineTotal = decimal.Round(
            normalizedUnitPrice * normalizedQuantity,
            CatalogMoney.Scale,
            MidpointRounding.AwayFromZero);

        return new OrderItem(
            CreateId(),
            orderId,
            productId,
            normalizedVariant,
            sellerId,
            productName,
            variantName,
            sku,
            imageUrl,
            normalizedQuantity,
            normalizedUnitPrice,
            CatalogMoney.RequireAmount(lineTotal),
            normalizedCurrency);
    }

    public static int RequireQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.")
            {
                Code = OrderErrorCodes.InvalidQuantity
            };
        }

        return quantity;
    }

    private static Guid? NormalizeVariantId(Guid? variantId)
    {
        if (variantId is null)
        {
            return null;
        }

        if (variantId.Value == Guid.Empty)
        {
            throw new DomainException("Variant is invalid.") { Code = "invalid_snapshot" };
        }

        return variantId;
    }

    private static string RequireText(string? value, int maxLength, string message)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 || trimmed.Length > maxLength)
        {
            throw new DomainException(message) { Code = "invalid_snapshot" };
        }

        return trimmed;
    }

    private static string? OptionalText(string? value, int maxLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException(message) { Code = "invalid_snapshot" };
        }

        return trimmed;
    }
}
