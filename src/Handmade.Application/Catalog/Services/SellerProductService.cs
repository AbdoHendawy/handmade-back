using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Storage;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Catalog;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Catalog.Services;

public interface ISellerProductService
{
    Task<PagedResult<ProductResponse>> ListMineAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetMineAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateAsync(
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> SubmitAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> CancelSubmitAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> ArchiveAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductResponse> RestoreAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductImageResponse> AddImageAsync(
        Guid productId,
        AddProductImageRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductImageResponse> UploadImageAsync(
        Guid productId,
        Stream content,
        string? contentType,
        long length,
        bool isPrimary,
        int? sortOrder,
        CancellationToken cancellationToken = default);

    Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);

    Task<ProductResponse> SetPrimaryImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> ReorderImagesAsync(
        Guid productId,
        ReorderProductImagesRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponse> AddVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponse> UpdateVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default);

    Task<ProductResponse> SetStockAsync(
        Guid productId,
        SetStockRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponse> SetVariantStockAsync(
        Guid productId,
        Guid variantId,
        SetStockRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SellerProductService : ISellerProductService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<SellerProductService> _logger;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;
    private readonly IValidator<AddProductImageRequest> _imageValidator;
    private readonly IValidator<ReorderProductImagesRequest> _reorderValidator;
    private readonly IValidator<CreateProductVariantRequest> _createVariantValidator;
    private readonly IValidator<UpdateProductVariantRequest> _updateVariantValidator;
    private readonly IValidator<SetStockRequest> _stockValidator;

    public SellerProductService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        INotificationPublisher notifications,
        IFileStorage fileStorage,
        ILogger<SellerProductService> logger,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator,
        IValidator<AddProductImageRequest> imageValidator,
        IValidator<ReorderProductImagesRequest> reorderValidator,
        IValidator<CreateProductVariantRequest> createVariantValidator,
        IValidator<UpdateProductVariantRequest> updateVariantValidator,
        IValidator<SetStockRequest> stockValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _fileStorage = fileStorage;
        _logger = logger;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _imageValidator = imageValidator;
        _reorderValidator = reorderValidator;
        _createVariantValidator = createVariantValidator;
        _updateVariantValidator = updateVariantValidator;
        _stockValidator = stockValidator;
    }

    public async Task<PagedResult<ProductResponse>> ListMineAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        SellerProfile seller = await RequireActiveSellerAsync(cancellationToken);
        IQueryable<Product> query = _db.Products.Where(p => p.SellerId == seller.Id);
        if (TryParseStatus(status, out ProductStatus parsed))
        {
            query = query.Where(p => p.Status == parsed);
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

    public async Task<ProductResponse> GetMineAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createValidator], cancellationToken);
        SellerProfile seller = await RequireActiveSellerAsync(cancellationToken);
        await RequireActiveCategoryAsync(request.CategoryId, cancellationToken);

        string slugSource = string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug;
        string slug = await CatalogPersistence.UniqueProductSlugAsync(_db, slugSource, null, cancellationToken);
        Product product = Product.Create(
            seller.Id,
            request.CategoryId,
            request.Name,
            slug,
            request.Description,
            request.Price,
            request.Currency ?? CatalogMoney.DefaultCurrency,
            _clock.UtcNow);
        product.SetStock(request.StockQuantity);

        _db.Products.Add(product);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        await RequireActiveCategoryAsync(request.CategoryId, cancellationToken);

        string slugSource = string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug;
        string slug = await CatalogPersistence.UniqueProductSlugAsync(_db, slugSource, product.Id, cancellationToken);
        product.UpdateDetails(
            request.Name,
            request.Description,
            request.CategoryId,
            request.Price,
            request.Currency ?? product.Currency);
        product.ReplaceSlug(slug);
        product.SetStock(request.StockQuantity);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken);
        if (!product.CanDelete)
        {
            throw new ConflictException("Only draft or rejected products can be deleted. Archive published products instead.")
            {
                Code = CatalogErrorCodes.InvalidStateTransition
            };
        }

        _db.Products.Remove(product);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
    }

    public async Task<ProductResponse> SubmitAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        await RequireActiveCategoryAsync(product.CategoryId, cancellationToken);
        bool hasImage = await _db.ProductImages.AnyAsync(i => i.ProductId == product.Id, cancellationToken);
        product.Submit(_clock.UtcNow, hasImage);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.PublishToUserAsync(
            new CreateUserNotificationRequest(
                seller.UserId,
                NotificationTypes.ProductSubmitted,
                "Product submitted for review",
                $"“{product.Name}” is waiting for admin review.",
                $"{NotificationTypes.ProductSubmitted}:{product.Id:D}"),
            cancellationToken);
        await _notifications.PublishToRoleAsync(
            RoleNames.Admin,
            NotificationTypes.ProductSubmitted,
            "Product pending review",
            $"A seller submitted “{product.Name}” for review.",
            $"{NotificationTypes.ProductSubmitted}:admin:{product.Id:D}",
            cancellationToken: cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> CancelSubmitAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        product.CancelSubmission();
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> ArchiveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        product.Archive(_clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> RestoreAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        product.Restore(_clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductImageResponse> AddImageAsync(
        Guid productId,
        AddProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_imageValidator], cancellationToken);
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();

        List<ProductImage> existing = await _db.ProductImages
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        int order = request.SortOrder ?? (existing.Count == 0 ? 1 : existing.Max(i => i.SortOrder) + 1);
        bool primary = request.IsPrimary || existing.Count == 0;
        if (primary)
        {
            foreach (ProductImage image in existing)
            {
                image.ClearPrimary();
            }
        }

        ProductImage created = ProductImage.Create(product.Id, request.StorageKey, request.Url, order, primary);
        _db.ProductImages.Add(created);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(created);
    }

    public async Task<ProductImageResponse> UploadImageAsync(
        Guid productId,
        Stream content,
        string? contentType,
        long length,
        bool isPrimary,
        int? sortOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        Stream payload = content;
        if (!content.CanSeek)
        {
            MemoryStream copy = new();
            await content.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            payload = copy;
            length = copy.Length;
        }

        string detectedType = ProductImageFileRules.Validate(payload, contentType, length);
        if (sortOrder is < 1)
        {
            throw new DomainException("Sort order must be at least 1.") { Code = CatalogErrorCodes.InvalidSortOrder };
        }

        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();

        string storageKey = ProductImageFileRules.CreateStorageKey(detectedType);
        if (payload.CanSeek)
        {
            payload.Position = 0;
        }

        storageKey = await _fileStorage.UploadAsync(payload, storageKey, detectedType, cancellationToken);
        try
        {
            Uri url = await _fileStorage.GetUrlAsync(storageKey, cancellationToken);
            List<ProductImage> existing = await _db.ProductImages
                .Where(i => i.ProductId == product.Id)
                .ToListAsync(cancellationToken);
            int order = sortOrder ?? (existing.Count == 0 ? 1 : existing.Max(i => i.SortOrder) + 1);
            bool primary = isPrimary || existing.Count == 0;
            if (primary)
            {
                foreach (ProductImage image in existing)
                {
                    image.ClearPrimary();
                }
            }

            ProductImage created = ProductImage.Create(product.Id, storageKey, url.ToString(), order, primary);
            _db.ProductImages.Add(created);
            await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
            return CatalogMapping.ToResponse(created);
        }
        catch
        {
            try
            {
                await _fileStorage.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception cleanup)
            {
                _logger.LogWarning(cleanup, "Failed to delete uploaded object {StorageKey} after persistence failure", storageKey);
            }

            throw;
        }
    }

    public async Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
    {
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        ProductImage image = await _db.ProductImages
                                   .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == product.Id, cancellationToken)
                               ?? throw new NotFoundException("ProductImage", imageId);
        bool wasPrimary = image.IsPrimary;
        _db.ProductImages.Remove(image);
        if (wasPrimary)
        {
            ProductImage? next = await _db.ProductImages
                .Where(i => i.ProductId == product.Id && i.Id != imageId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);
            next?.MarkPrimary();
        }

        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
    }

    public async Task<ProductResponse> SetPrimaryImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        List<ProductImage> images = await _db.ProductImages
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        ProductImage target = images.FirstOrDefault(i => i.Id == imageId)
                              ?? throw new NotFoundException("ProductImage", imageId);
        foreach (ProductImage image in images)
        {
            if (image.Id == target.Id)
            {
                image.MarkPrimary();
            }
            else
            {
                image.ClearPrimary();
            }
        }

        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductResponse> ReorderImagesAsync(
        Guid productId,
        ReorderProductImagesRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_reorderValidator], cancellationToken);
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        List<ProductImage> images = await _db.ProductImages
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        if (request.ImageIds.Count != images.Count || request.ImageIds.Distinct().Count() != request.ImageIds.Count)
        {
            throw new DomainException("Reorder must include every image exactly once.")
            {
                Code = CatalogErrorCodes.InvalidImageReorder
            };
        }

        for (int i = 0; i < request.ImageIds.Count; i++)
        {
            ProductImage image = images.FirstOrDefault(img => img.Id == request.ImageIds[i])
                                 ?? throw new NotFoundException("ProductImage", request.ImageIds[i]);
            image.SetSortOrder(i + 1);
        }

        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductVariantResponse> AddVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createVariantValidator], cancellationToken);
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        string sku = ProductVariant.RequireSku(request.Sku);
        await EnsureSkuUniqueAsync(sku, null, cancellationToken);
        ProductVariant variant = ProductVariant.Create(
            product.Id,
            request.Name,
            sku,
            request.Price,
            request.Currency ?? product.Currency);
        variant.SetStock(request.StockQuantity);
        _db.ProductVariants.Add(variant);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(variant);
    }

    public async Task<ProductVariantResponse> UpdateVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateVariantValidator], cancellationToken);
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        ProductVariant variant = await _db.ProductVariants
                                       .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id, cancellationToken)
                                   ?? throw new NotFoundException("ProductVariant", variantId);
        string sku = ProductVariant.RequireSku(request.Sku);
        await EnsureSkuUniqueAsync(sku, variant.Id, cancellationToken);
        variant.Update(request.Name, sku, request.Price, request.Currency ?? product.Currency);
        variant.SetStock(request.StockQuantity);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(variant);
    }

    public async Task DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default)
    {
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        ProductVariant variant = await _db.ProductVariants
                                       .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id, cancellationToken)
                                   ?? throw new NotFoundException("ProductVariant", variantId);
        _db.ProductVariants.Remove(variant);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
    }

    public async Task<ProductResponse> SetStockAsync(
        Guid productId,
        SetStockRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_stockValidator], cancellationToken);
        (Product product, SellerProfile seller) = await LoadOwnedAsync(productId, cancellationToken);
        product.SetStock(request.StockQuantity);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return await MapAsync(product, seller, cancellationToken);
    }

    public async Task<ProductVariantResponse> SetVariantStockAsync(
        Guid productId,
        Guid variantId,
        SetStockRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_stockValidator], cancellationToken);
        (Product product, _) = await LoadOwnedAsync(productId, cancellationToken, trackProduct: false);
        product.AssertEditable();
        ProductVariant variant = await _db.ProductVariants
                                       .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id, cancellationToken)
                                   ?? throw new NotFoundException("ProductVariant", variantId);
        variant.SetStock(request.StockQuantity);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(variant);
    }

    private async Task<(Product Product, SellerProfile Seller)> LoadOwnedAsync(
        Guid productId,
        CancellationToken cancellationToken,
        bool trackProduct = true)
    {
        SellerProfile seller = await RequireActiveSellerAsync(cancellationToken);
        IQueryable<Product> products = trackProduct ? _db.Products : _db.Products.AsNoTracking();
        Product product = await products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                          ?? throw new NotFoundException("Product", productId);
        product.EnsureOwnedBy(seller.Id);
        return (product, seller);
    }

    private async Task<SellerProfile> RequireActiveSellerAsync(CancellationToken cancellationToken)
    {
        Guid userId = RequireUserId();
        SellerProfile seller = await _db.SellerProfiles
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                               ?? throw new ForbiddenException("An active seller profile is required.")
                               {
                                   Code = SellerErrorCodes.ProfileNotActive
                               };

        if (!seller.IsActive)
        {
            throw new ForbiddenException("An active seller profile is required.")
            {
                Code = SellerErrorCodes.ProfileNotActive
            };
        }

        return seller;
    }

    private async Task<Category> RequireActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        Category category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
                            ?? throw new NotFoundException("Category", categoryId);
        if (!category.IsActive)
        {
            throw new DomainException("Inactive categories cannot be used for products.")
            {
                Code = CatalogErrorCodes.CategoryInactive
            };
        }

        return category;
    }

    private async Task EnsureSkuUniqueAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        bool exists = await _db.ProductVariants.AnyAsync(
            v => v.Sku == sku && (exceptVariantId == null || v.Id != exceptVariantId),
            cancellationToken);
        if (exists)
        {
            throw new ConflictException("SKU must be unique.") { Code = CatalogErrorCodes.DuplicateSku };
        }
    }

    private async Task<ProductResponse> MapAsync(
        Product product,
        SellerProfile seller,
        CancellationToken cancellationToken)
    {
        Category category = await _db.Categories.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == product.CategoryId, cancellationToken)
                            ?? throw new NotFoundException("Category", product.CategoryId);
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

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }

    private static bool TryParseStatus(string? status, out ProductStatus parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, ignoreCase: true, out parsed);
    }
}
