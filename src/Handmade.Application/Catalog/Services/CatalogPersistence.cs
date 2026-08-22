using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

internal static class CatalogPersistence
{
    public static async Task SaveChangesAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The resource was modified by another operation.")
            {
                Code = CatalogErrorCodes.ConcurrencyConflict
            };
        }
    }

    public static async Task<string> UniqueProductSlugAsync(
        IApplicationDbContext db,
        string desired,
        Guid? exceptProductId,
        CancellationToken cancellationToken)
    {
        string baseSlug = CatalogSlug.FromName(desired);
        List<string> existing = await db.Products
            .Where(p => exceptProductId == null || p.Id != exceptProductId)
            .Where(p => p.Slug == baseSlug || p.Slug.StartsWith(baseSlug + "-"))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        return CatalogSlug.NextUnique(baseSlug, existing.ToHashSet(StringComparer.Ordinal));
    }

    public static async Task<string> UniqueCategorySlugAsync(
        IApplicationDbContext db,
        string desired,
        Guid? exceptCategoryId,
        CancellationToken cancellationToken)
    {
        string baseSlug = string.IsNullOrWhiteSpace(desired) ? CatalogSlug.FromName("category") : CatalogSlug.FromName(desired);
        List<string> existing = await db.Categories
            .Where(c => exceptCategoryId == null || c.Id != exceptCategoryId)
            .Where(c => c.Slug == baseSlug || c.Slug.StartsWith(baseSlug + "-"))
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);
        return CatalogSlug.NextUnique(baseSlug, existing.ToHashSet(StringComparer.Ordinal));
    }

    public static bool WouldCreateCycle(Guid categoryId, Guid? newParentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        Guid? current = newParentId;
        int depth = 0;
        while (current is Guid parent)
        {
            if (parent == categoryId)
            {
                return true;
            }

            if (++depth > Category.MaxDepth)
            {
                return true;
            }

            if (!parents.TryGetValue(parent, out current))
            {
                break;
            }
        }

        return false;
    }

    public static int DepthOf(Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        int depth = 0;
        Guid? current = parentId;
        while (current is Guid parent && parents.TryGetValue(parent, out current))
        {
            depth++;
            if (depth > Category.MaxDepth)
            {
                return depth;
            }
        }

        return depth;
    }

    public static async Task<(
        Dictionary<Guid, List<ProductImage>> Images,
        Dictionary<Guid, List<ProductVariant>> Variants)> LoadChildrenAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return ([], []);
        }

        List<ProductImage> images = await db.ProductImages
            .Where(i => productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);
        List<ProductVariant> variants = await db.ProductVariants
            .Where(v => productIds.Contains(v.ProductId))
            .ToListAsync(cancellationToken);

        return (
            images.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.ToList()),
            variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList()));
    }
}
