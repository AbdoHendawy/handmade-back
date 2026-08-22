using Handmade.Application.Orders.Services;
using Handmade.Domain.Catalog;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Application.Tests;

public sealed class CheckoutConcurrencyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void InventoryXminOnFirstAttempt_Retries()
    {
        Product product = CreateProduct();
        CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
            0,
            [product],
            new HashSet<Guid> { product.Id },
            new HashSet<Guid>());

        Assert.Equal(CheckoutConcurrencyAction.Retry, action);
        Assert.Equal(2, CheckoutConcurrency.MaxAttempts);
    }

    [Fact]
    public void InventoryXminOnSecondAttempt_IsOrdersConflict()
    {
        Product product = CreateProduct();
        CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
            1,
            [product],
            new HashSet<Guid> { product.Id },
            new HashSet<Guid>());

        Assert.Equal(CheckoutConcurrencyAction.OrdersConflict, action);
    }

    [Fact]
    public void InventoryXminNeverHasThirdAttempt()
    {
        Product product = CreateProduct();
        CheckoutConcurrencyAction third = CheckoutConcurrency.Decide(
            2,
            [product],
            new HashSet<Guid> { product.Id },
            new HashSet<Guid>());

        Assert.Equal(CheckoutConcurrencyAction.OrdersConflict, third);
    }

    [Fact]
    public void UnrelatedEntity_IsRethrown_NotOrdersConflict()
    {
        OrderGroup group = OrderGroup.Create(
            Guid.CreateVersion7(),
            "Nour",
            "Hassan",
            "nour@example.com",
            OrderDeliverySnapshot.Create(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                null,
                "Cairo",
                "Cairo",
                null,
                null),
            "EGP",
            Now);
        Product product = CreateProduct();

        CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
            0,
            [group],
            new HashSet<Guid> { product.Id },
            new HashSet<Guid>());

        Assert.Equal(CheckoutConcurrencyAction.Rethrow, action);
    }

    [Fact]
    public void MixedInventoryAndUnrelated_IsRethrown()
    {
        Product product = CreateProduct();
        OrderGroup group = OrderGroup.Create(
            Guid.CreateVersion7(),
            "Nour",
            "Hassan",
            "nour@example.com",
            OrderDeliverySnapshot.Create(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                null,
                "Cairo",
                "Cairo",
                null,
                null),
            "EGP",
            Now);

        CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
            0,
            [product, group],
            new HashSet<Guid> { product.Id },
            new HashSet<Guid>());

        Assert.Equal(CheckoutConcurrencyAction.Rethrow, action);
    }

    [Fact]
    public void VariantInventoryXmin_IsExpected()
    {
        Product product = CreateProduct();
        ProductVariant variant = ProductVariant.Create(product.Id, "Large", "ORD-L1", 40m, "EGP");

        CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
            0,
            [variant],
            new HashSet<Guid>(),
            new HashSet<Guid> { variant.Id });

        Assert.Equal(CheckoutConcurrencyAction.Retry, action);
    }

    private static Product CreateProduct()
    {
        return Product.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Handmade Cup",
            "handmade-cup-" + Guid.NewGuid().ToString("N")[..8],
            "A handmade ceramic cup for daily use.",
            50m,
            "EGP",
            Now);
    }
}
