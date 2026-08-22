using Handmade.Domain.Catalog;

namespace Handmade.Application.Cart.Services;

public readonly record struct CartLineAmounts(decimal UnitPrice, int Quantity, bool IsAvailable);

public readonly record struct CartTotals(decimal Subtotal, decimal Total, int ItemCount, int DistinctItemCount);

public static class CartCalculator
{
    public static decimal LineSubtotal(decimal unitPrice, int quantity)
    {
        return decimal.Round(unitPrice * quantity, CatalogMoney.Scale, MidpointRounding.AwayFromZero);
    }

    public static CartTotals Compute(IReadOnlyList<CartLineAmounts> lines)
    {
        decimal subtotal = 0m;
        decimal total = 0m;
        int itemCount = 0;
        foreach (CartLineAmounts line in lines)
        {
            decimal lineSubtotal = LineSubtotal(line.UnitPrice, line.Quantity);
            subtotal += lineSubtotal;
            if (line.IsAvailable)
            {
                total += lineSubtotal;
            }

            itemCount += line.Quantity;
        }

        return new CartTotals(subtotal, total, itemCount, lines.Count);
    }
}
