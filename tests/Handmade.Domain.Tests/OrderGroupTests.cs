using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.Events;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Domain.Tests;

public sealed class OrderGroupTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_StartsPlaced()
    {
        OrderGroup group = CreateGroup();

        Assert.Equal(OrderGroupStatus.Placed, group.Status);
        Assert.Equal(0, group.Number);
        Assert.Equal(0m, group.Subtotal);
        Assert.Equal(0m, group.Total);
    }

    [Fact]
    public void Create_StoresCustomerId()
    {
        OrderGroup group = CreateGroup();

        Assert.Equal(CustomerId, group.CustomerId);
        Assert.Equal("Nour", group.CustomerFirstName);
        Assert.Equal("Hassan", group.CustomerLastName);
        Assert.Equal("nour@example.com", group.CustomerEmail);
    }

    [Fact]
    public void Create_StoresCurrency()
    {
        OrderGroup group = OrderGroup.Create(
            CustomerId,
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "egp",
            Now);

        Assert.Equal("EGP", group.Currency);
    }

    [Fact]
    public void Create_PreservesDeliverySnapshot()
    {
        OrderDeliverySnapshot delivery = OrderDeliverySnapshot.Create(
            "  Nour Hassan  ",
            "  +201001234567  ",
            "  12 Nile Street  ",
            "  Apt 4  ",
            "  Cairo  ",
            "  Giza  ",
            "  11511  ",
            "  Leave at the door  ");

        OrderGroup group = OrderGroup.Create(
            CustomerId,
            "Nour",
            "Hassan",
            "nour@example.com",
            delivery,
            "EGP",
            Now);

        Assert.Equal("Nour Hassan", group.RecipientName);
        Assert.Equal("+201001234567", group.Phone);
        Assert.Equal("12 Nile Street", group.AddressLine1);
        Assert.Equal("Apt 4", group.AddressLine2);
        Assert.Equal("Cairo", group.City);
        Assert.Equal("Giza", group.Governorate);
        Assert.Equal("11511", group.PostalCode);
        Assert.Equal("Leave at the door", group.Notes);
    }

    [Fact]
    public void Create_RaisesOrderGroupPlaced_WithoutDispatching()
    {
        OrderGroup group = CreateGroup();

        OrderGroupPlaced raised = Assert.IsType<OrderGroupPlaced>(Assert.Single(group.DomainEvents));
        Assert.Equal(group.Id, raised.OrderGroupId);
        Assert.Equal(CustomerId, raised.CustomerId);
        Assert.Equal(Now, raised.OccurredAt);

        group.ClearDomainEvents();
        Assert.Empty(group.DomainEvents);
    }

    [Fact]
    public void ApplyTotalsFromOrders_SumsSeparateSellerOrders()
    {
        OrderGroup group = CreateGroup();
        Guid sellerA = Guid.CreateVersion7();
        Guid sellerB = Guid.CreateVersion7();

        Order first = CreateOrder(group, sellerA, "Atelier Nile");
        Order second = CreateOrder(group, sellerB, "Desert Loom");
        first.AddItem(Guid.CreateVersion7(), null, sellerA, "Cup", null, null, null, 2, 40m, "EGP");
        second.AddItem(Guid.CreateVersion7(), null, sellerB, "Rug", null, null, null, 1, 70m, "EGP");

        group.ApplyTotalsFromOrders([first, second]);

        Assert.Equal(150m, group.Subtotal);
        Assert.Equal(150m, group.Total);
    }

    [Fact]
    public void ApplyTotalsFromOrders_CurrencyMismatch_IsRejected()
    {
        OrderGroup group = CreateGroup();
        Guid sellerId = Guid.CreateVersion7();
        Order order = Order.Create(
            group.Id,
            CustomerId,
            sellerId,
            "Atelier Nile",
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "USD",
            Now);

        DomainException ex = Assert.Throws<DomainException>(() => group.ApplyTotalsFromOrders([order]));
        Assert.Equal(OrderErrorCodes.CurrencyMismatch, ex.Code);
    }

    private static OrderGroup CreateGroup()
    {
        return OrderGroup.Create(
            CustomerId,
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "EGP",
            Now);
    }

    private static Order CreateOrder(OrderGroup group, Guid sellerId, string sellerName)
    {
        return Order.Create(
            group.Id,
            CustomerId,
            sellerId,
            sellerName,
            "Nour",
            "Hassan",
            "nour@example.com",
            SampleDelivery(),
            "EGP",
            Now);
    }

    private static OrderDeliverySnapshot SampleDelivery()
    {
        return OrderDeliverySnapshot.Create(
            "Nour Hassan",
            "+201001234567",
            "12 Nile Street",
            "Apt 4",
            "Cairo",
            "Cairo",
            "11511",
            "Leave at the door");
    }
}
