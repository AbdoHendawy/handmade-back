using Handmade.Application.Orders;

namespace Handmade.Application.Tests;

public sealed class OrderCalculatorTests
{
    [Fact]
    public void LineTotal_IsUnitPriceTimesQuantity()
    {
        Assert.Equal(200m, OrderCalculator.LineTotal(100m, 2));
        Assert.Equal(37.50m, OrderCalculator.LineTotal(12.50m, 3));
        Assert.Equal(0.33m, OrderCalculator.LineTotal(0.11m, 3));
    }

    [Fact]
    public void ComputeOrder_SubtotalIsSumOfLineTotals()
    {
        OrderTotals totals = OrderCalculator.ComputeOrder(
        [
            new OrderLineAmounts(100m, 2),
            new OrderLineAmounts(50m, 3)
        ]);

        Assert.Equal(350m, totals.Subtotal);
    }

    [Fact]
    public void ComputeOrder_TotalEqualsSubtotal()
    {
        OrderTotals totals = OrderCalculator.ComputeOrder(
        [
            new OrderLineAmounts(12.50m, 3),
            new OrderLineAmounts(10m, 1)
        ]);

        Assert.Equal(totals.Subtotal, totals.Total);
        Assert.Equal(47.50m, totals.Total);
    }

    [Fact]
    public void ComputeGroup_SubtotalIsSumOfOrderSubtotals()
    {
        OrderGroupTotals totals = OrderCalculator.ComputeGroup(
        [
            new OrderTotals(80m, 80m),
            new OrderTotals(70m, 70m)
        ]);

        Assert.Equal(150m, totals.Subtotal);
    }

    [Fact]
    public void ComputeGroup_TotalIsSumOfOrderTotals()
    {
        OrderGroupTotals totals = OrderCalculator.ComputeGroup(
        [
            new OrderTotals(80m, 80m),
            new OrderTotals(70m, 70m)
        ]);

        Assert.Equal(150m, totals.Total);
        Assert.Equal(totals.Subtotal, totals.Total);
    }

    [Fact]
    public void ComputeGroup_MultipleSellers_AreSeparateOrderTotals()
    {
        OrderTotals sellerA = OrderCalculator.ComputeOrder([new OrderLineAmounts(40m, 2)]);
        OrderTotals sellerB = OrderCalculator.ComputeOrder([new OrderLineAmounts(70m, 1)]);

        Assert.Equal(80m, sellerA.Subtotal);
        Assert.Equal(70m, sellerB.Subtotal);

        OrderGroupTotals group = OrderCalculator.ComputeGroup([sellerA, sellerB]);
        Assert.Equal(150m, group.Subtotal);
        Assert.Equal(150m, group.Total);
    }

    [Fact]
    public void ComputeOrder_EmptyLines_IsZero()
    {
        OrderTotals totals = OrderCalculator.ComputeOrder([]);
        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Total);
    }
}
