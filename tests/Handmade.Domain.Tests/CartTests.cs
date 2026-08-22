using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using CartAggregate = Handmade.Domain.Cart.Cart;

namespace Handmade.Domain.Tests;

public sealed class CartTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ProductA = Guid.CreateVersion7();
    private static readonly Guid ProductB = Guid.CreateVersion7();
    private static readonly Guid VariantA = Guid.CreateVersion7();

    [Fact]
    public void Create_AssignsUser_AndEmptyItems()
    {
        CartAggregate cart = CartAggregate.Create(UserId);

        Assert.Equal(UserId, cart.UserId);
        Assert.Empty(cart.Items);
        Assert.NotEqual(Guid.Empty, cart.Id);
    }

    [Fact]
    public void AddItem_CreatesLine()
    {
        CartAggregate cart = CartAggregate.Create(UserId);

        CartItem item = cart.AddOrIncrease(ProductA, null, 2, 100m, "EGP");

        Assert.Single(cart.Items);
        Assert.Equal(ProductA, item.ProductId);
        Assert.Null(item.VariantId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(100m, item.PriceSnapshot);
        Assert.Equal("EGP", item.Currency);
    }

    [Fact]
    public void AddSameProductTwice_IncreasesQuantity()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP");

        CartItem item = cart.AddOrIncrease(ProductA, null, 2, 120m, "EGP");

        Assert.Single(cart.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(120m, item.PriceSnapshot);
    }

    [Fact]
    public void AddSameProductDifferentVariant_CreatesSeparateLines()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, VariantA, 1, 90m, "EGP");
        cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP");

        Assert.Equal(2, cart.Items.Count);
    }

    [Fact]
    public void UpdateQuantity_SetsAbsoluteQuantity()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 2, 100m, "EGP");

        CartItem item = cart.UpdateQuantity(ProductA, null, 5);

        Assert.Equal(5, item.Quantity);
        Assert.Single(cart.Items);
    }

    [Fact]
    public void DecreaseQuantity_SetsLowerValue()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 5, 100m, "EGP");

        cart.UpdateQuantity(ProductA, null, 1);

        Assert.Equal(1, cart.Items.Single().Quantity);
    }

    [Fact]
    public void RemoveItem_RemovesOnlyThatLine()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP");
        cart.AddOrIncrease(ProductB, null, 2, 50m, "EGP");

        cart.RemoveItem(ProductA, null);

        Assert.Single(cart.Items);
        Assert.Equal(ProductB, cart.Items.Single().ProductId);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP");
        cart.AddOrIncrease(ProductB, null, 3, 50m, "EGP");

        IReadOnlyList<CartItem> removed = cart.Clear();

        Assert.Empty(cart.Items);
        Assert.Equal(2, removed.Count);
    }

    [Fact]
    public void InvalidQuantity_Throws()
    {
        CartAggregate cart = CartAggregate.Create(UserId);

        DomainException zero = Assert.Throws<DomainException>(() => cart.AddOrIncrease(ProductA, null, 0, 100m, "EGP"));
        Assert.Equal(CartErrorCodes.InvalidQuantity, zero.Code);

        DomainException over = Assert.Throws<DomainException>(() =>
            cart.AddOrIncrease(ProductA, null, CartLimits.MaxQuantityPerItem + 1, 100m, "EGP"));
        Assert.Equal(CartErrorCodes.InvalidQuantity, over.Code);
    }

    [Fact]
    public void IncreaseBeyondMax_Throws()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, CartLimits.MaxQuantityPerItem, 100m, "EGP");

        DomainException ex = Assert.Throws<DomainException>(() => cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP"));
        Assert.Equal(CartErrorCodes.InvalidQuantity, ex.Code);
    }

    [Fact]
    public void MixedCurrency_Throws()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        cart.AddOrIncrease(ProductA, null, 1, 100m, "EGP");

        DomainException ex = Assert.Throws<DomainException>(() => cart.AddOrIncrease(ProductB, null, 1, 10m, "USD"));
        Assert.Equal(CartErrorCodes.CurrencyMismatch, ex.Code);
    }

    [Fact]
    public void NegativePrice_Throws()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        DomainException ex = Assert.Throws<DomainException>(() => cart.AddOrIncrease(ProductA, null, 1, -1m, "EGP"));
        Assert.Equal(CatalogErrorCodes.InvalidPrice, ex.Code);
    }

    [Fact]
    public void RemoveMissingItem_ThrowsNotFound()
    {
        CartAggregate cart = CartAggregate.Create(UserId);
        Assert.Throws<NotFoundException>(() => cart.RemoveItem(ProductA, null));
    }

    [Fact]
    public void EmptyUser_Throws()
    {
        DomainException ex = Assert.Throws<DomainException>(() => CartAggregate.Create(Guid.Empty));
        Assert.Equal("invalid_user", ex.Code);
    }
}
