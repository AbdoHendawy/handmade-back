using Handmade.Domain.Orders;

namespace Handmade.Domain.Tests;

public sealed class OrderStatusTests
{
    [Fact]
    public void OrderStatus_ContainsOnlyPlaced()
    {
        OrderStatus[] values = Enum.GetValues<OrderStatus>();

        Assert.Equal(new[] { OrderStatus.Placed }, values);
        Assert.Equal(0, (int)OrderStatus.Placed);
    }

    [Fact]
    public void OrderGroupStatus_ContainsOnlyPlaced()
    {
        OrderGroupStatus[] values = Enum.GetValues<OrderGroupStatus>();

        Assert.Equal(new[] { OrderGroupStatus.Placed }, values);
        Assert.Equal(0, (int)OrderGroupStatus.Placed);
    }
}
