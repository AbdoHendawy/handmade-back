using System.Collections;
using System.Linq.Expressions;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Catalog.Services;
using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using CartEntity = Handmade.Domain.Cart.Cart;

namespace Handmade.Application.Tests;

public sealed class ProductInventoryTests
{
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task IncrementAsync_ProductLine_IncreasesProductStock()
    {
        Product product = ReadyProduct();
        product.SetStock(10);
        InventoryTestDb db = InventoryTestDb.FromProducts(product);
        ProductInventory inventory = new(db);

        await inventory.IncrementAsync([new StockIncrement(product.Id, null, 2)]);

        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_VariantLine_IncreasesVariantStockOnly()
    {
        Product product = ReadyProduct();
        product.SetStock(10);
        ProductVariant variant = product.AddVariant("Small", "BRC-S", 100m, "EGP");
        variant.SetStock(5);
        InventoryTestDb db = InventoryTestDb.FromProducts(product);
        ProductInventory inventory = new(db);

        await inventory.IncrementAsync([new StockIncrement(product.Id, variant.Id, 3)]);

        Assert.Equal(10, product.StockQuantity);
        Assert.Equal(8, variant.StockQuantity);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_NullVariantId_RestoresProduct_EvenWhenVariantsExist()
    {
        Product product = ReadyProduct();
        product.SetStock(10);
        ProductVariant variant = product.AddVariant("Small", "BRC-S", 100m, "EGP");
        variant.SetStock(0);
        InventoryTestDb db = InventoryTestDb.FromProducts(product);
        ProductInventory inventory = new(db);

        await inventory.IncrementAsync([new StockIncrement(product.Id, null, 2)]);

        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(0, variant.StockQuantity);
    }

    [Fact]
    public async Task IncrementAsync_VariantBelongsToOtherProduct_Fails_AndDoesNotChangeStock()
    {
        Product productOne = ReadyProduct("one", "product-one");
        productOne.SetStock(10);
        Product productTwo = ReadyProduct("two", "product-two");
        ProductVariant foreignVariant = productTwo.AddVariant("Large", "BRC-L", 110m, "EGP");
        foreignVariant.SetStock(4);
        InventoryTestDb db = InventoryTestDb.FromProducts(productOne, productTwo);
        ProductInventory inventory = new(db);

        NotFoundException ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            inventory.IncrementAsync([new StockIncrement(productOne.Id, foreignVariant.Id, 2)]));

        Assert.Equal(CatalogErrorCodes.VariantNotFound, ex.Code);
        Assert.Equal(10, productOne.StockQuantity);
        Assert.Equal(4, foreignVariant.StockQuantity);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_MissingProduct_Fails()
    {
        InventoryTestDb db = InventoryTestDb.FromProducts();
        ProductInventory inventory = new(db);
        Guid missingId = Guid.CreateVersion7();

        NotFoundException ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            inventory.IncrementAsync([new StockIncrement(missingId, null, 1)]));

        Assert.Equal(CatalogErrorCodes.ProductNotFound, ex.Code);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_MissingVariant_Fails_AndDoesNotChangeProductStock()
    {
        Product product = ReadyProduct();
        product.SetStock(6);
        InventoryTestDb db = InventoryTestDb.FromProducts(product);
        ProductInventory inventory = new(db);
        Guid missingVariantId = Guid.CreateVersion7();

        NotFoundException ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            inventory.IncrementAsync([new StockIncrement(product.Id, missingVariantId, 1)]));

        Assert.Equal(CatalogErrorCodes.VariantNotFound, ex.Code);
        Assert.Equal(6, product.StockQuantity);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_MultipleLines_RestoresEachQuantityOnce()
    {
        Product productA = ReadyProduct("alpha", "alpha-stock");
        productA.SetStock(10);
        Product productC = ReadyProduct("gamma", "gamma-stock");
        productC.SetStock(1);
        Product variantHost = ReadyProduct("beta", "beta-stock");
        variantHost.SetStock(7);
        ProductVariant variantB = variantHost.AddVariant("Medium", "BRC-M", 105m, "EGP");
        variantB.SetStock(3);
        InventoryTestDb db = InventoryTestDb.FromProducts(productA, variantHost, productC);
        ProductInventory inventory = new(db);

        await inventory.IncrementAsync(
        [
            new StockIncrement(productA.Id, null, 2),
            new StockIncrement(variantHost.Id, variantB.Id, 3),
            new StockIncrement(productC.Id, null, 1)
        ]);

        Assert.Equal(12, productA.StockQuantity);
        Assert.Equal(6, variantB.StockQuantity);
        Assert.Equal(7, variantHost.StockQuantity);
        Assert.Equal(2, productC.StockQuantity);
        Assert.Equal(0, db.SaveChangesCalls);
    }

    [Fact]
    public async Task IncrementAsync_DoesNotCallSaveChanges()
    {
        Product product = ReadyProduct();
        product.SetStock(1);
        InventoryTestDb db = InventoryTestDb.FromProducts(product);
        ProductInventory inventory = new(db);

        await inventory.IncrementAsync([]);
        await inventory.IncrementAsync([new StockIncrement(product.Id, null, 1)]);

        Assert.Equal(0, db.SaveChangesCalls);
        Assert.Equal(2, product.StockQuantity);
    }

    private static Product ReadyProduct(string name = "Handmade Bracelet", string slug = "handmade-bracelet")
    {
        Product product = Product.Create(
            SellerId,
            CategoryId,
            name,
            slug,
            "A handmade leather bracelet.",
            120m,
            "EGP",
            Now);
        product.AddImage("main.jpg", null, 1, true);
        return product;
    }
}

internal sealed class InventoryTestDb : IApplicationDbContext
{
    private InventoryTestDb(QueryableDbSet<Product> products, QueryableDbSet<ProductVariant> variants)
    {
        Products = products;
        ProductVariants = variants;
    }

    public static InventoryTestDb FromProducts(params Product[] products)
    {
        List<ProductVariant> variants = products.SelectMany(p => p.Variants).ToList();
        return new InventoryTestDb(new QueryableDbSet<Product>(products), new QueryableDbSet<ProductVariant>(variants));
    }

    public int SaveChangesCalls { get; private set; }

    public DbSet<Product> Products { get; }

    public DbSet<ProductVariant> ProductVariants { get; }

    public DbSet<User> Users => throw new NotSupportedException();

    public DbSet<Role> Roles => throw new NotSupportedException();

    public DbSet<UserRole> UserRoles => throw new NotSupportedException();

    public DbSet<ExternalLogin> ExternalLogins => throw new NotSupportedException();

    public DbSet<RefreshToken> RefreshTokens => throw new NotSupportedException();

    public DbSet<SellerApplication> SellerApplications => throw new NotSupportedException();

    public DbSet<SellerProfile> SellerProfiles => throw new NotSupportedException();

    public DbSet<Notification> Notifications => throw new NotSupportedException();

    public DbSet<Category> Categories => throw new NotSupportedException();

    public DbSet<ProductImage> ProductImages => throw new NotSupportedException();

    public DbSet<CartEntity> Carts => throw new NotSupportedException();

    public DbSet<CartItem> CartItems => throw new NotSupportedException();

    public DbSet<OrderGroup> OrderGroups => throw new NotSupportedException();

    public DbSet<Order> Orders => throw new NotSupportedException();

    public DbSet<OrderItem> OrderItems => throw new NotSupportedException();

    public void ClearTrackedEntities()
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class QueryableDbSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>
    where T : class
{
    private readonly IQueryable<T> _linq;
    private readonly IQueryProvider _provider;

    public QueryableDbSet(IEnumerable<T> items)
    {
        _linq = items.AsQueryable();
        _provider = new AsyncQueryProvider(_linq.Provider);
    }

    public override IEntityType EntityType => throw new NotSupportedException();

    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new AsyncEnumerator<T>(_linq.GetEnumerator());
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _linq.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _linq.GetEnumerator();

    Type IQueryable.ElementType => _linq.ElementType;

    Expression IQueryable.Expression => _linq.Expression;

    IQueryProvider IQueryable.Provider => _provider;
}

internal sealed class AsyncQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>
{
    private readonly IQueryable<T> _query;
    private readonly IQueryProvider _provider;

    public AsyncQueryable(Expression expression, IQueryProvider inner)
    {
        _query = new EnumerableQuery<T>(expression);
        _provider = new AsyncQueryProvider(inner);
    }

    public Type ElementType => typeof(T);

    public Expression Expression => _query.Expression;

    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator() => _query.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new AsyncEnumerator<T>(_query.GetEnumerator());
    }
}

internal sealed class AsyncQueryProvider : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public AsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new AsyncQueryable<object>(expression, _inner);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new AsyncQueryable<TElement>(expression, _inner);
    }

    public object? Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        object? result = Execute(expression);
        Type resultType = typeof(TResult).IsGenericType
            ? typeof(TResult).GetGenericArguments()[0]
            : typeof(TResult);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal sealed class AsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public AsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
