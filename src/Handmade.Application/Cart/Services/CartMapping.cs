using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Domain.Cart;

namespace Handmade.Application.Cart.Services;

internal static class CartMapping
{
    public static CartResponse Empty()
    {
        return new CartResponse(null, [], 0m, 0m, null, 0, 0);
    }

    public static CartResponse ToResponse(
        Guid cartId,
        IReadOnlyList<CartItem> items,
        IReadOnlyDictionary<(Guid ProductId, Guid? VariantId), ProductPurchaseInfo> products)
    {
        List<CartItemResponse> lines = items
            .Select(item => ToItem(item, products.GetValueOrDefault((item.ProductId, item.VariantId))))
            .ToList();
        CartTotals totals = CartCalculator.Compute(
            lines.Select(l => new CartLineAmounts(l.UnitPrice, l.Quantity, l.IsAvailable)).ToList());
        return new CartResponse(
            cartId,
            lines,
            totals.Subtotal,
            totals.Total,
            lines.Count == 0 ? null : lines[0].Currency,
            totals.ItemCount,
            totals.DistinctItemCount);
    }

    private static CartItemResponse ToItem(CartItem item, ProductPurchaseInfo? product)
    {
        bool exists = product?.Exists == true;
        bool available = product?.IsPurchasable == true;
        decimal unitPrice = exists ? product!.UnitPrice : item.PriceSnapshot;
        string currency = exists ? product!.Currency : item.Currency;
        bool priceChanged = available &&
                            (unitPrice != item.PriceSnapshot ||
                             !string.Equals(currency, item.Currency, StringComparison.OrdinalIgnoreCase));

        return new CartItemResponse(
            item.ProductId,
            item.VariantId,
            product?.VariantName,
            exists ? product!.Name : string.Empty,
            product?.ImageUrl,
            product?.SellerName ?? string.Empty,
            item.Quantity,
            unitPrice,
            currency,
            CartCalculator.LineSubtotal(unitPrice, item.Quantity),
            available,
            priceChanged,
            available ? null : product?.UnavailabilityReason ?? ProductPurchaseQuery.ReasonNotFound);
    }
}
