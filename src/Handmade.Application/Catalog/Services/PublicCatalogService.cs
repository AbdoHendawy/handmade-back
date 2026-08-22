using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

public interface IPublicCatalogService
{
    Task<IReadOnlyList<CategoryTreeResponse>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<PublicProductResponse>> ListProductsAsync(
        Guid? categoryId,
        Guid? sellerId,
        string? search,
        string? sort,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<PublicProductResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}

public sealed class PublicCatalogService : IPublicCatalogService
{
    private readonly IApplicationDbContext _db;

    public PublicCatalogService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryTreeResponse>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        List<Category> categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return AdminCategoryService.BuildTree(categories, includeInactive: false);
    }

    public async Task<PagedResult<PublicProductResponse>> ListProductsAsync(
        Guid? categoryId,
        Guid? sellerId,
        string? search,
        string? sort,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        if (!CatalogSortOptions.IsAllowed(sort))
        {
            throw new DomainException("Sort must be newest, priceAsc, or priceDesc.") { Code = "invalid_sort" };
        }

        IQueryable<Product> query = _db.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published);

        if (categoryId.HasValue)
        {
            HashSet<Guid> categoryIds = await CollectCategoryAndDescendantsAsync(categoryId.Value, cancellationToken);
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (sellerId.HasValue)
        {
            query = query.Where(p => p.SellerId == sellerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term));
        }

        query = ApplySort(query, sort);

        int total = await query.CountAsync(cancellationToken);
        List<Product> items = await query
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PublicProductResponse>(
            await MapManyAsync(items, cancellationToken),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<PublicProductResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        string normalized = CatalogSlug.Require(slug);
        Product product = await _db.Products
                              .AsNoTracking()
                              .FirstOrDefaultAsync(
                                  p => p.Slug == normalized && p.Status == ProductStatus.Published,
                                  cancellationToken)
                          ?? throw new NotFoundException("Product", normalized);

        return (await MapManyAsync([product], cancellationToken))[0];
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string? sort)
    {
        string value = sort?.Trim() ?? CatalogSortOptions.Newest;
        if (value.Equals(CatalogSortOptions.PriceAsc, StringComparison.OrdinalIgnoreCase))
        {
            return query.OrderBy(p => p.Price).ThenByDescending(p => p.PublishedAt);
        }

        if (value.Equals(CatalogSortOptions.PriceDesc, StringComparison.OrdinalIgnoreCase))
        {
            return query.OrderByDescending(p => p.Price).ThenByDescending(p => p.PublishedAt);
        }

        return query.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.CreatedAt);
    }

    private async Task<HashSet<Guid>> CollectCategoryAndDescendantsAsync(Guid rootId, CancellationToken cancellationToken)
    {
        List<Category> all = await _db.Categories.AsNoTracking().ToListAsync(cancellationToken);
        if (all.All(c => c.Id != rootId))
        {
            throw new NotFoundException("Category", rootId);
        }

        HashSet<Guid> ids = [rootId];
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (Category category in all)
            {
                if (category.ParentCategoryId is Guid parent && ids.Contains(parent) && ids.Add(category.Id))
                {
                    grew = true;
                }
            }
        }

        return ids;
    }

    private async Task<IReadOnlyList<PublicProductResponse>> MapManyAsync(
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return [];
        }

        HashSet<Guid> categoryIds = products.Select(p => p.CategoryId).ToHashSet();
        HashSet<Guid> sellerIds = products.Select(p => p.SellerId).ToHashSet();
        Dictionary<Guid, Category> categories = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        Dictionary<Guid, SellerProfile> sellers = await _db.SellerProfiles.AsNoTracking()
            .Where(s => sellerIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        (Dictionary<Guid, List<ProductImage>> images, Dictionary<Guid, List<ProductVariant>> variants) =
            await CatalogPersistence.LoadChildrenAsync(_db, products.Select(p => p.Id).ToList(), cancellationToken);
        return products.Select(p => CatalogMapping.ToPublic(
            p,
            categories[p.CategoryId],
            sellers[p.SellerId],
            images.GetValueOrDefault(p.Id),
            variants.GetValueOrDefault(p.Id))).ToList();
    }
}
