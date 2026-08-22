using Handmade.Domain.Catalog;

namespace Handmade.Application.Orders;

public readonly record struct OrderLineAmounts(decimal UnitPrice, int Quantity);

public readonly record struct OrderTotals(decimal Subtotal, decimal Total);

public readonly record struct OrderGroupTotals(decimal Subtotal, decimal Total);

public static class OrderCalculator
{
    public static decimal LineTotal(decimal unitPrice, int quantity)
    {
        return decimal.Round(unitPrice * quantity, CatalogMoney.Scale, MidpointRounding.AwayFromZero);
    }

    public static OrderTotals ComputeOrder(IReadOnlyList<OrderLineAmounts> lines)
    {
        decimal subtotal = 0m;
        foreach (OrderLineAmounts line in lines)
        {
            subtotal += LineTotal(line.UnitPrice, line.Quantity);
        }

        return new OrderTotals(subtotal, subtotal);
    }

    public static OrderGroupTotals ComputeGroup(IReadOnlyList<OrderTotals> orders)
    {
        decimal subtotal = 0m;
        decimal total = 0m;
        foreach (OrderTotals order in orders)
        {
            subtotal += order.Subtotal;
            total += order.Total;
        }

        return new OrderGroupTotals(subtotal, total);
    }
}
