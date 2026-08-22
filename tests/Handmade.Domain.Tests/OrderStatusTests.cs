using Handmade.Domain.Orders;

namespace Handmade.Domain.Tests;

public sealed class OrderStatusTests
{
    [Fact]
    public void OrderStatus_ContainsLifecycleValues_WithPlacedAsZero()
    {
        OrderStatus[] values = Enum.GetValues<OrderStatus>();

        Assert.Equal(
            [
                OrderStatus.Placed,
                OrderStatus.Confirmed,
                OrderStatus.Preparing,
                OrderStatus.Shipped,
                OrderStatus.Delivered,
                OrderStatus.Cancelled
            ],
            values);
        Assert.Equal(0, (int)OrderStatus.Placed);
        Assert.Equal(1, (int)OrderStatus.Confirmed);
        Assert.Equal(2, (int)OrderStatus.Preparing);
        Assert.Equal(3, (int)OrderStatus.Shipped);
        Assert.Equal(4, (int)OrderStatus.Delivered);
        Assert.Equal(5, (int)OrderStatus.Cancelled);
    }

    [Fact]
    public void OrderGroupStatus_ContainsOnlyPlaced()
    {
        OrderGroupStatus[] values = Enum.GetValues<OrderGroupStatus>();

        Assert.Equal(new[] { OrderGroupStatus.Placed }, values);
        Assert.Equal(0, (int)OrderGroupStatus.Placed);
    }

    [Fact]
    public void PaymentMethod_ContainsOnlyCashOnDelivery()
    {
        PaymentMethod[] values = Enum.GetValues<PaymentMethod>();

        Assert.Equal(new[] { PaymentMethod.CashOnDelivery }, values);
        Assert.Equal(0, (int)PaymentMethod.CashOnDelivery);
    }
}
