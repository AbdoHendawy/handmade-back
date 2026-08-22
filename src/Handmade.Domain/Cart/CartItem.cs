using Handmade.Domain.Catalog;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Cart;

public sealed class CartItem : Entity, IAuditable
{
    private CartItem()
    {
    }

    internal CartItem(
        Guid id,
        Guid cartId,
        Guid productId,
        Guid? variantId,
        int quantity,
        decimal priceSnapshot,
        string currency)
        : base(id)
    {
        CartId = cartId;
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
        PriceSnapshot = priceSnapshot;
        Currency = currency;
    }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? VariantId { get; private set; }

    public int Quantity { get; private set; }

    public decimal PriceSnapshot { get; private set; }

    public string Currency { get; private set; } = CatalogMoney.DefaultCurrency;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CartItem Create(
        Guid cartId,
        Guid productId,
        Guid? variantId,
        int quantity,
        decimal priceSnapshot,
        string currency)
    {
        if (cartId == Guid.Empty)
        {
            throw new DomainException("Cart is required.") { Code = CartErrorCodes.CartItemNotFound };
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Product is required.") { Code = CatalogErrorCodes.ProductNotFound };
        }

        return new CartItem(
            CreateId(),
            cartId,
            productId,
            variantId,
            RequireQuantity(quantity),
            CatalogMoney.RequireAmount(priceSnapshot),
            CatalogMoney.RequireCurrency(currency));
    }

    public void Increase(int quantity)
    {
        Quantity = RequireQuantity(Quantity + quantity);
    }

    public void SetQuantity(int quantity)
    {
        Quantity = RequireQuantity(quantity);
    }

    public void ReplacePriceSnapshot(decimal priceSnapshot, string currency)
    {
        PriceSnapshot = CatalogMoney.RequireAmount(priceSnapshot);
        Currency = CatalogMoney.RequireCurrency(currency);
    }

    public static bool SameLine(Guid productId, Guid? variantId, Guid otherProductId, Guid? otherVariantId)
    {
        return productId == otherProductId && variantId == otherVariantId;
    }

    public static int RequireQuantity(int quantity)
    {
        if (quantity < 1 || quantity > CartLimits.MaxQuantityPerItem)
        {
            throw new DomainException($"Quantity must be between 1 and {CartLimits.MaxQuantityPerItem}.")
            {
                Code = CartErrorCodes.InvalidQuantity
            };
        }

        return quantity;
    }
}
