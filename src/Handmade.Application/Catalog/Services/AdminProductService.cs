using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Notifications;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

public interface IAdminProductService
{
    Task<PagedResult<ProductResponse>> ListAsync(
        string? status,
        Guid? sellerId,
        Guid? categoryId,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> ApproveAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> RejectAsync(
        Guid productId,
        RejectProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> ArchiveAsync(Guid productId, CancellationToken cancellationToken = default);
}

public sealed class AdminProductService : IAdminProductService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;
    private readonly IValidator<RejectProductRequest> _rejectValidator;

    public AdminProductService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        INotificationPublisher notifications,
        IValidator<RejectProductRequest> rejectValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _rejectValidator = rejectValidator;
    }

    public async Task<PagedResult<ProductResponse>> ListAsync(
        string? status,
        Guid? sellerId,
        Guid? categoryId,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        IQueryable<Product> query = _db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out ProductStatus parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        if (sellerId.HasValue)
        {
            query = query.Where(p => p.SellerId == sellerId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        int total = await query.CountAsync(cancellationToken);
        List<Product> items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductResponse>(
            await MapManyAsync(items, cancellationToken),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<ProductResponse> GetAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        Product product = await LoadAsync(productId, cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    public async Task<ProductResponse> ApproveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        Guid adminId = RequireAdmin();
        Product product = await LoadAsync(productId, cancellationToken);
        product.Approve(adminId, _clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        await NotifySellerAsync(
            product,
            NotificationTypes.ProductApproved,
            "Your product is published",
            $"“{product.Name}” is now visible in the catalog.",
            cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    public async Task<ProductResponse> RejectAsync(
        Guid productId,
        RejectProductRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid adminId = RequireAdmin();
        await ValidationBehavior.ValidateAndThrowAsync(request, [_rejectValidator], cancellationToken);
        Product product = await LoadAsync(productId, cancellationToken);
        product.Reject(adminId, request.Reason, _clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        await NotifySellerAsync(
            product,
            NotificationTypes.ProductRejected,
            "Your product was not approved",
            request.Reason,
            cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    public async Task<ProductResponse> ArchiveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        Product product = await LoadAsync(productId, cancellationToken);
        product.Archive(_clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, cancellationToken);
    }

    private async Task NotifySellerAsync(
        Product product,
        string type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        SellerProfile? seller = await _db.SellerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == product.SellerId, cancellationToken);
        if (seller is null)
        {
            return;
        }

        await _notifications.PublishToUserAsync(
            new CreateUserNotificationRequest(
                seller.UserId,
                type,
                title,
                body,
                $"{type}:{product.Id:D}"),
            cancellationToken);
    }

    private async Task<Product> LoadAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
               ?? throw new NotFoundException("Product", productId);
    }

    private async Task<ProductResponse> MapAsync(Product product, CancellationToken cancellationToken)
    {
        Category category = await _db.Categories.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == product.CategoryId, cancellationToken)
                            ?? throw new NotFoundException("Category", product.CategoryId);
        SellerProfile seller = await _db.SellerProfiles.AsNoTracking()
                                   .FirstOrDefaultAsync(s => s.Id == product.SellerId, cancellationToken)
                               ?? throw new NotFoundException("SellerProfile", product.SellerId);
        (Dictionary<Guid, List<ProductImage>> images, Dictionary<Guid, List<ProductVariant>> variants) =
            await CatalogPersistence.LoadChildrenAsync(_db, [product.Id], cancellationToken);
        return CatalogMapping.ToResponse(
            product,
            category,
            seller,
            images.GetValueOrDefault(product.Id),
            variants.GetValueOrDefault(product.Id));
    }

    private async Task<IReadOnlyList<ProductResponse>> MapManyAsync(
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
        return products.Select(p => CatalogMapping.ToResponse(
            p,
            categories[p.CategoryId],
            sellers[p.SellerId],
            images.GetValueOrDefault(p.Id),
            variants.GetValueOrDefault(p.Id))).ToList();
    }

    private Guid RequireAdmin()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }
}
