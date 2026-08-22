using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

public sealed record StockDecrement(Guid ProductId, Guid? VariantId, int Quantity);

public interface IProductInventory
{
    Task DecrementAsync(IReadOnlyList<StockDecrement> lines, CancellationToken cancellationToken = default);
}

public sealed class ProductInventory : IProductInventory
{
    private readonly IApplicationDbContext _db;

    public ProductInventory(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task DecrementAsync(
        IReadOnlyList<StockDecrement> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return;
        }

        List<Guid> productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        Dictionary<Guid, Product> products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        List<ProductVariant> variants = await _db.ProductVariants
            .Where(v => productIds.Contains(v.ProductId))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, List<ProductVariant>> variantsByProduct = variants
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (StockDecrement line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out Product? product))
            {
                throw new NotFoundException("Product", line.ProductId)
                {
                    Code = CatalogErrorCodes.ProductNotFound
                };
            }

            List<ProductVariant> productVariants = variantsByProduct.GetValueOrDefault(line.ProductId) ?? [];
            if (productVariants.Count > 0)
            {
                if (line.VariantId is not Guid variantId)
                {
                    throw new DomainException("This product requires a variant.")
                    {
                        Code = CatalogErrorCodes.VariantRequired
                    };
                }

                ProductVariant variant = productVariants.FirstOrDefault(v => v.Id == variantId)
                    ?? throw new NotFoundException("ProductVariant", variantId)
                    {
                        Code = CatalogErrorCodes.VariantNotFound
                    };
                variant.DecrementStock(line.Quantity);
                continue;
            }

            if (line.VariantId is Guid unexpectedVariant)
            {
                throw new NotFoundException("ProductVariant", unexpectedVariant)
                {
                    Code = CatalogErrorCodes.VariantNotFound
                };
            }

            product.DecrementStock(line.Quantity);
        }
    }
}
