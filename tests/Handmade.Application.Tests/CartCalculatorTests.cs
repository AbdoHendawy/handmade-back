using Handmade.Application.Cart.Services;

namespace Handmade.Application.Tests;

public sealed class CartCalculatorTests
{
    [Fact]
    public void LineSubtotal_IsUnitPriceTimesQuantity()
    {
        Assert.Equal(200m, CartCalculator.LineSubtotal(100m, 2));
        Assert.Equal(37.50m, CartCalculator.LineSubtotal(12.50m, 3));
    }

    [Fact]
    public void Compute_SumsQuantitiesAndSubtotals()
    {
        CartTotals totals = CartCalculator.Compute(
        [
            new CartLineAmounts(100m, 2, true),
            new CartLineAmounts(50m, 3, true)
        ]);

        Assert.Equal(350m, totals.Subtotal);
        Assert.Equal(350m, totals.Total);
        Assert.Equal(5, totals.ItemCount);
        Assert.Equal(2, totals.DistinctItemCount);
    }

    [Fact]
    public void Compute_TotalExcludesUnavailableLines()
    {
        CartTotals totals = CartCalculator.Compute(
        [
            new CartLineAmounts(100m, 2, true),
            new CartLineAmounts(50m, 3, false)
        ]);

        Assert.Equal(350m, totals.Subtotal);
        Assert.Equal(200m, totals.Total);
        Assert.Equal(5, totals.ItemCount);
        Assert.Equal(2, totals.DistinctItemCount);
    }

    [Fact]
    public void Compute_EmptyCart_IsZero()
    {
        CartTotals totals = CartCalculator.Compute([]);
        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Total);
        Assert.Equal(0, totals.ItemCount);
        Assert.Equal(0, totals.DistinctItemCount);
    }
}
