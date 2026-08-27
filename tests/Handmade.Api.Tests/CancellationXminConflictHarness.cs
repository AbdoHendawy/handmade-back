using Handmade.Application.Catalog.Services;
using Handmade.Domain.Catalog;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

public sealed class CancellationXminConflictHarness
{
    private ArmedConflict? _armed;

    public IDisposable ArmProduct(Guid productId, int setStockTo)
    {
        _armed = new ArmedConflict(productId, null, setStockTo);
        return new Disarm(this);
    }

    public IDisposable ArmVariant(Guid variantId, int setStockTo)
    {
        _armed = new ArmedConflict(null, variantId, setStockTo);
        return new Disarm(this);
    }

    internal ArmedConflict? Consume()
    {
        ArmedConflict? current = _armed;
        _armed = null;
        return current;
    }

    internal void Clear()
    {
        _armed = null;
    }

    private sealed class Disarm(CancellationXminConflictHarness owner) : IDisposable
    {
        public void Dispose() => owner.Clear();
    }
}

internal sealed record ArmedConflict(Guid? ProductId, Guid? VariantId, int SetStockTo);

internal sealed class CancellationXminConflictInventory : IProductInventory
{
    private readonly IProductInventory _inner;
    private readonly CancellationXminConflictHarness _harness;
    private readonly IServiceScopeFactory _scopeFactory;

    public CancellationXminConflictInventory(
        IProductInventory inner,
        CancellationXminConflictHarness harness,
        IServiceScopeFactory scopeFactory)
    {
        _inner = inner;
        _harness = harness;
        _scopeFactory = scopeFactory;
    }

    public Task DecrementAsync(
        IReadOnlyList<StockDecrement> lines,
        CancellationToken cancellationToken = default)
    {
        return _inner.DecrementAsync(lines, cancellationToken);
    }

    public async Task IncrementAsync(
        IReadOnlyList<StockIncrement> lines,
        CancellationToken cancellationToken = default)
    {
        await _inner.IncrementAsync(lines, cancellationToken);
        ArmedConflict? conflict = _harness.Consume();
        if (conflict is null)
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        if (conflict.VariantId is Guid variantId)
        {
            ProductVariant variant = await db.ProductVariants
                .SingleAsync(v => v.Id == variantId, cancellationToken);
            variant.SetStock(conflict.SetStockTo);
        }
        else
        {
            Product product = await db.Products
                .SingleAsync(p => p.Id == conflict.ProductId!.Value, cancellationToken);
            product.SetStock(conflict.SetStockTo);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
