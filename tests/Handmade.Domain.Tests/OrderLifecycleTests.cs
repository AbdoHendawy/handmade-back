using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.Events;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Domain.Tests;

public sealed class OrderLifecycleTests
{
    private static readonly Guid OrderGroupId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void SiblingOrders_AdvanceIndependently()
    {
        Order first = CreateOrder();
        Order second = Order.Create(
            OrderGroupId,
            CustomerId,
            Guid.CreateVersion7(),
            "Desert Loom",
            "Nour",
            "Hassan",
            "nour@example.com",
            OrderDeliverySnapshot.Create(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                "Apt 4",
                "Cairo",
                "Cairo",
                "11511",
                "Leave at the door"),
            "EGP",
            Now);

        first.Confirm(Now);
        first.Prepare(Now);
        first.Ship(Now);
        second.Confirm(Now);
        second.Prepare(Now);

        Assert.Equal(OrderStatus.Shipped, first.Status);
        Assert.Equal(OrderStatus.Preparing, second.Status);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.OrderGroupId, second.OrderGroupId);
    }

    [Fact]
    public void Create_StartsPlaced()
    {
        Order order = CreateOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
    }

    [Fact]
    public void Status_HasNoPublicSetter()
    {
        Assert.False(typeof(Order).GetProperty(nameof(Order.Status))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void Placed_ToConfirmed_Succeeds()
    {
        Order order = CreateOrder();
        order.ClearDomainEvents();

        order.Confirm(Now);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        OrderConfirmed confirmed = Assert.IsType<OrderConfirmed>(Assert.Single(order.DomainEvents));
        AssertEvent(order, confirmed.OrderId, confirmed.OrderGroupId, confirmed.SellerId, confirmed.CustomerId, confirmed.OccurredAt);
    }

    [Fact]
    public void Confirmed_ToPreparing_Succeeds()
    {
        Order order = Confirmed();
        order.ClearDomainEvents();

        order.Prepare(Now);

        Assert.Equal(OrderStatus.Preparing, order.Status);
        OrderPreparing preparing = Assert.IsType<OrderPreparing>(Assert.Single(order.DomainEvents));
        AssertEvent(order, preparing.OrderId, preparing.OrderGroupId, preparing.SellerId, preparing.CustomerId, preparing.OccurredAt);
    }

    [Fact]
    public void Preparing_ToShipped_Succeeds()
    {
        Order order = Preparing();
        order.ClearDomainEvents();

        order.Ship(Now);

        Assert.Equal(OrderStatus.Shipped, order.Status);
        OrderShipped shipped = Assert.IsType<OrderShipped>(Assert.Single(order.DomainEvents));
        AssertEvent(order, shipped.OrderId, shipped.OrderGroupId, shipped.SellerId, shipped.CustomerId, shipped.OccurredAt);
    }

    [Fact]
    public void Shipped_ToDelivered_Succeeds()
    {
        Order order = Shipped();
        order.ClearDomainEvents();

        order.Deliver(Now);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        OrderDelivered delivered = Assert.IsType<OrderDelivered>(Assert.Single(order.DomainEvents));
        AssertEvent(order, delivered.OrderId, delivered.OrderGroupId, delivered.SellerId, delivered.CustomerId, delivered.OccurredAt);
    }

    [Fact]
    public void Placed_ToCancelled_Succeeds()
    {
        Order order = CreateOrder();
        order.ClearDomainEvents();

        order.Cancel(Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        OrderCancelled cancelled = Assert.IsType<OrderCancelled>(Assert.Single(order.DomainEvents));
        AssertEvent(order, cancelled.OrderId, cancelled.OrderGroupId, cancelled.SellerId, cancelled.CustomerId, cancelled.OccurredAt);
    }

    [Fact]
    public void HappyPath_ReachesDelivered()
    {
        Order order = CreateOrder();

        order.Confirm(Now);
        order.Prepare(Now);
        order.Ship(Now);
        order.Deliver(Now);

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Placed, nameof(Order.Prepare))]
    [InlineData(OrderStatus.Placed, nameof(Order.Ship))]
    [InlineData(OrderStatus.Placed, nameof(Order.Deliver))]
    [InlineData(OrderStatus.Confirmed, nameof(Order.Ship))]
    [InlineData(OrderStatus.Confirmed, nameof(Order.Deliver))]
    [InlineData(OrderStatus.Confirmed, nameof(Order.Cancel))]
    [InlineData(OrderStatus.Confirmed, nameof(Order.Confirm))]
    [InlineData(OrderStatus.Preparing, nameof(Order.Deliver))]
    [InlineData(OrderStatus.Preparing, nameof(Order.Cancel))]
    [InlineData(OrderStatus.Preparing, nameof(Order.Confirm))]
    [InlineData(OrderStatus.Preparing, nameof(Order.Prepare))]
    [InlineData(OrderStatus.Shipped, nameof(Order.Cancel))]
    [InlineData(OrderStatus.Shipped, nameof(Order.Confirm))]
    [InlineData(OrderStatus.Shipped, nameof(Order.Prepare))]
    [InlineData(OrderStatus.Shipped, nameof(Order.Ship))]
    [InlineData(OrderStatus.Delivered, nameof(Order.Cancel))]
    [InlineData(OrderStatus.Delivered, nameof(Order.Confirm))]
    [InlineData(OrderStatus.Delivered, nameof(Order.Prepare))]
    [InlineData(OrderStatus.Delivered, nameof(Order.Ship))]
    [InlineData(OrderStatus.Delivered, nameof(Order.Deliver))]
    [InlineData(OrderStatus.Cancelled, nameof(Order.Confirm))]
    [InlineData(OrderStatus.Cancelled, nameof(Order.Prepare))]
    [InlineData(OrderStatus.Cancelled, nameof(Order.Ship))]
    [InlineData(OrderStatus.Cancelled, nameof(Order.Deliver))]
    [InlineData(OrderStatus.Cancelled, nameof(Order.Cancel))]
    public void InvalidTransition_IsRejected(OrderStatus start, string action)
    {
        Order order = At(start);

        ConflictException ex = Assert.Throws<ConflictException>(() => Invoke(order, action));

        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.Equal(start, order.Status);
    }

    [Fact]
    public void Create_StillRaisesOrderPlaced_WithoutDispatching()
    {
        Order order = CreateOrder();

        OrderPlaced raised = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id, raised.OrderId);
        Assert.Equal(OrderGroupId, raised.OrderGroupId);
        Assert.Equal(SellerId, raised.SellerId);
        Assert.Equal(CustomerId, raised.CustomerId);
        Assert.Equal(Now, raised.OccurredAt);
    }

    private static void AssertEvent(
        Order order,
        Guid orderId,
        Guid orderGroupId,
        Guid sellerId,
        Guid customerId,
        DateTimeOffset occurredAt)
    {
        Assert.Equal(order.Id, orderId);
        Assert.Equal(OrderGroupId, orderGroupId);
        Assert.Equal(SellerId, sellerId);
        Assert.Equal(CustomerId, customerId);
        Assert.Equal(Now, occurredAt);
    }

    private static Order Confirmed()
    {
        Order order = CreateOrder();
        order.Confirm(Now);
        return order;
    }

    private static Order Preparing()
    {
        Order order = Confirmed();
        order.Prepare(Now);
        return order;
    }

    private static Order Shipped()
    {
        Order order = Preparing();
        order.Ship(Now);
        return order;
    }

    private static Order At(OrderStatus status)
    {
        Order order = CreateOrder();
        if (status == OrderStatus.Placed)
        {
            return order;
        }

        order.Confirm(Now);
        if (status == OrderStatus.Confirmed)
        {
            return order;
        }

        order.Prepare(Now);
        if (status == OrderStatus.Preparing)
        {
            return order;
        }

        order.Ship(Now);
        if (status == OrderStatus.Shipped)
        {
            return order;
        }

        order.Deliver(Now);
        if (status == OrderStatus.Delivered)
        {
            return order;
        }

        order = CreateOrder();
        order.Cancel(Now);
        return order;
    }

    private static void Invoke(Order order, string action)
    {
        switch (action)
        {
            case nameof(Order.Confirm):
                order.Confirm(Now);
                break;
            case nameof(Order.Prepare):
                order.Prepare(Now);
                break;
            case nameof(Order.Ship):
                order.Ship(Now);
                break;
            case nameof(Order.Deliver):
                order.Deliver(Now);
                break;
            case nameof(Order.Cancel):
                order.Cancel(Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static Order CreateOrder()
    {
        return Order.Create(
            OrderGroupId,
            CustomerId,
            SellerId,
            "Atelier Nile",
            "Nour",
            "Hassan",
            "nour@example.com",
            OrderDeliverySnapshot.Create(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                "Apt 4",
                "Cairo",
                "Cairo",
                "11511",
                "Leave at the door"),
            "EGP",
            Now);
    }
}
