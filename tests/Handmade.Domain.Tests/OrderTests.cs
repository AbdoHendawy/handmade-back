using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.Events;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Domain.Tests;

public sealed class OrderTests
{
    private static readonly Guid OrderGroupId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly Guid OtherSellerId = Guid.CreateVersion7();
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly Guid VariantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_StartsPlaced_WithSingleSeller()
    {
        Order order = CreateOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(SellerId, order.SellerId);
        Assert.Equal(OrderGroupId, order.OrderGroupId);
        Assert.Equal(CustomerId, order.CustomerId);
        Assert.Equal("EGP", order.Currency);
        Assert.Equal("Atelier Nile", order.SellerNameSnapshot);
        Assert.Empty(order.Items);
        Assert.Equal(0m, order.Subtotal);
        Assert.Equal(0m, order.Total);
        Assert.Equal(0, order.Number);
    }

    [Fact]
    public void AddItem_MatchingSeller_IsAccepted()
    {
        Order order = CreateOrder();

        OrderItem item = AddNonVariantItem(order, SellerId, 2, 50m);

        Assert.Single(order.Items);
        Assert.Equal(SellerId, item.SellerId);
        Assert.Equal(order.SellerId, item.SellerId);
        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(100m, order.Total);
    }

    [Fact]
    public void AddItem_DifferentSeller_IsRejected()
    {
        Order order = CreateOrder();

        DomainException ex = Assert.Throws<DomainException>(() =>
            AddNonVariantItem(order, OtherSellerId, 1, 50m));

        Assert.Equal(OrderErrorCodes.SellerMismatch, ex.Code);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_InvalidQuantity_IsRejected()
    {
        Order order = CreateOrder();

        DomainException ex = Assert.Throws<DomainException>(() =>
            AddNonVariantItem(order, SellerId, 0, 50m));

        Assert.Equal(OrderErrorCodes.InvalidQuantity, ex.Code);

        DomainException negative = Assert.Throws<DomainException>(() =>
            AddNonVariantItem(order, SellerId, -2, 50m));

        Assert.Equal(OrderErrorCodes.InvalidQuantity, negative.Code);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_PreservesSnapshotValues()
    {
        Order order = CreateOrder();

        OrderItem item = order.AddItem(
            ProductId,
            VariantId,
            SellerId,
            "  Handmade Cup  ",
            "  Large  ",
            "  CUP-L  ",
            "  https://cdn.example/cup.jpg  ",
            3,
            12.50m,
            "egp");

        Assert.Equal(ProductId, item.ProductId);
        Assert.Equal(VariantId, item.VariantId);
        Assert.Equal("Handmade Cup", item.ProductNameSnapshot);
        Assert.Equal("Large", item.VariantNameSnapshot);
        Assert.Equal("CUP-L", item.SkuSnapshot);
        Assert.Equal("https://cdn.example/cup.jpg", item.ImageUrlSnapshot);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(12.50m, item.UnitPrice);
        Assert.Equal(37.50m, item.LineTotal);
        Assert.Equal("EGP", item.Currency);
        Assert.Equal(SellerId, item.SellerId);
        Assert.Equal(order.Id, item.OrderId);
    }

    [Fact]
    public void AddItem_NonVariantLine_HasNullSkuSnapshot()
    {
        Order order = CreateOrder();

        OrderItem item = AddNonVariantItem(order, SellerId, 1, 80m);

        Assert.Null(item.VariantId);
        Assert.Null(item.VariantNameSnapshot);
        Assert.Null(item.SkuSnapshot);
    }

    [Fact]
    public void AddItem_CurrencyMismatch_IsRejected()
    {
        Order order = CreateOrder();

        DomainException ex = Assert.Throws<DomainException>(() =>
            order.AddItem(
                ProductId,
                null,
                SellerId,
                "Handmade Cup",
                null,
                null,
                null,
                1,
                50m,
                "USD"));

        Assert.Equal(OrderErrorCodes.CurrencyMismatch, ex.Code);
    }

    [Fact]
    public void AddItem_NegativeUnitPrice_IsRejected()
    {
        Order order = CreateOrder();

        DomainException ex = Assert.Throws<DomainException>(() =>
            AddNonVariantItem(order, SellerId, 1, -1m));

        Assert.Equal(CatalogErrorCodes.InvalidPrice, ex.Code);
    }

    [Fact]
    public void MultipleSellers_AreSeparateOrders()
    {
        Order first = CreateOrder(SellerId, "Atelier Nile");
        Order second = CreateOrder(OtherSellerId, "Desert Loom");

        AddNonVariantItem(first, SellerId, 2, 40m);
        AddNonVariantItem(second, OtherSellerId, 1, 70m);

        Assert.Equal(SellerId, first.SellerId);
        Assert.Equal(OtherSellerId, second.SellerId);
        Assert.All(first.Items, item => Assert.Equal(first.SellerId, item.SellerId));
        Assert.All(second.Items, item => Assert.Equal(second.SellerId, item.SellerId));
        Assert.Equal(80m, first.Subtotal);
        Assert.Equal(70m, second.Subtotal);

        DomainException ex = Assert.Throws<DomainException>(() =>
            AddNonVariantItem(first, OtherSellerId, 1, 10m));
        Assert.Equal(OrderErrorCodes.SellerMismatch, ex.Code);
    }

    [Fact]
    public void Create_RaisesOrderPlaced_WithoutDispatching()
    {
        Order order = CreateOrder();

        OrderPlaced raised = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id, raised.OrderId);
        Assert.Equal(OrderGroupId, raised.OrderGroupId);
        Assert.Equal(SellerId, raised.SellerId);
        Assert.Equal(CustomerId, raised.CustomerId);
        Assert.Equal(Now, raised.OccurredAt);

        order.ClearDomainEvents();
        Assert.Empty(order.DomainEvents);
    }

    private static Order CreateOrder() => CreateOrder(SellerId, "Atelier Nile");

    private static Order CreateOrder(Guid sellerId, string sellerName)
    {
        return Order.Create(
            OrderGroupId,
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

    private static OrderItem AddNonVariantItem(Order order, Guid sellerId, int quantity, decimal unitPrice)
    {
        return order.AddItem(
            ProductId,
            null,
            sellerId,
            "Handmade Cup",
            null,
            null,
            "https://cdn.example/cup.jpg",
            quantity,
            unitPrice,
            "EGP");
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
