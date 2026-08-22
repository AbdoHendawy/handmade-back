using Handmade.Domain.Catalog;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Cart;

public sealed class Cart : AggregateRoot, IAuditable
{
    private readonly List<CartItem> _items = [];

    private Cart()
    {
    }

    private Cart(Guid id, Guid userId)
        : base(id)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public static Cart Create(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User is required.") { Code = "invalid_user" };
        }

        return new Cart(CreateId(), userId);
    }

    public void RestoreItems(IEnumerable<CartItem> items)
    {
        _items.Clear();
        foreach (CartItem item in items)
        {
            if (item.CartId != Id)
            {
                throw new DomainException("Cart item does not belong to this cart.")
                {
                    Code = CartErrorCodes.CartItemNotFound
                };
            }

            _items.Add(item);
        }
    }

    public CartItem? FindItem(Guid productId, Guid? variantId)
    {
        return _items.FirstOrDefault(i => CartItem.SameLine(i.ProductId, i.VariantId, productId, variantId));
    }

    public CartItem AddOrIncrease(
        Guid productId,
        Guid? variantId,
        int quantity,
        decimal priceSnapshot,
        string currency)
    {
        int addQuantity = CartItem.RequireQuantity(quantity);
        string normalizedCurrency = CatalogMoney.RequireCurrency(currency);
        decimal normalizedPrice = CatalogMoney.RequireAmount(priceSnapshot);
        EnsureCurrency(normalizedCurrency);

        CartItem? existing = FindItem(productId, variantId);
        if (existing is not null)
        {
            existing.Increase(addQuantity);
            existing.ReplacePriceSnapshot(normalizedPrice, normalizedCurrency);
            return existing;
        }

        CartItem created = CartItem.Create(Id, productId, variantId, addQuantity, normalizedPrice, normalizedCurrency);
        _items.Add(created);
        return created;
    }

    public CartItem UpdateQuantity(Guid productId, Guid? variantId, int quantity)
    {
        CartItem item = RequireItem(productId, variantId);
        item.SetQuantity(quantity);
        return item;
    }

    public CartItem RemoveItem(Guid productId, Guid? variantId)
    {
        CartItem item = RequireItem(productId, variantId);
        _items.Remove(item);
        return item;
    }

    public IReadOnlyList<CartItem> Clear()
    {
        CartItem[] removed = [.. _items];
        _items.Clear();
        return removed;
    }

    private CartItem RequireItem(Guid productId, Guid? variantId)
    {
        return FindItem(productId, variantId)
               ?? throw new NotFoundException("CartItem", variantId is Guid variant
                   ? $"{productId}:{variant}"
                   : productId.ToString());
    }

    private void EnsureCurrency(string currency)
    {
        CartItem? existing = _items.FirstOrDefault();
        if (existing is not null &&
            !string.Equals(existing.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("A cart cannot mix currencies.")
            {
                Code = CartErrorCodes.CurrencyMismatch
            };
        }
    }
}
