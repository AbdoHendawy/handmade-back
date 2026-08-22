using FluentValidation;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Catalog.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Catalog.Services;

public interface IAdminCategoryService
{
    Task<IReadOnlyList<CategoryTreeResponse>> ListTreeAsync(CancellationToken cancellationToken = default);

    Task<CategoryResponse> GetAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> ActivateAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<CategoryResponse> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public sealed class AdminCategoryService : IAdminCategoryService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateCategoryRequest> _createValidator;
    private readonly IValidator<UpdateCategoryRequest> _updateValidator;

    public AdminCategoryService(
        IApplicationDbContext db,
        IClock clock,
        IValidator<CreateCategoryRequest> createValidator,
        IValidator<UpdateCategoryRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<CategoryTreeResponse>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        List<Category> categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return BuildTree(categories, includeInactive: true);
    }

    public async Task<CategoryResponse> GetAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        Category category = await LoadAsync(categoryId, cancellationToken);
        return CatalogMapping.ToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_createValidator], cancellationToken);
        await EnsureParentAsync(request.ParentCategoryId, cancellationToken);

        Dictionary<Guid, Guid?> parents = await LoadParentsAsync(cancellationToken);
        if (request.ParentCategoryId.HasValue &&
            CatalogPersistence.DepthOf(request.ParentCategoryId, parents) >= Category.MaxDepth)
        {
            throw new DomainException("Category hierarchy cannot exceed 5 levels.")
            {
                Code = CatalogErrorCodes.InvalidParent
            };
        }

        string slugSource = string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug;
        string slug = await CatalogPersistence.UniqueCategorySlugAsync(_db, slugSource, null, cancellationToken);
        Category category = Category.Create(request.Name, slug, request.Description, request.ParentCategoryId, _clock.UtcNow);
        _db.Categories.Add(category);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);
        Category category = await LoadAsync(categoryId, cancellationToken);
        await EnsureParentAsync(request.ParentCategoryId, cancellationToken);

        Dictionary<Guid, Guid?> parents = await LoadParentsAsync(cancellationToken);
        if (CatalogPersistence.WouldCreateCycle(categoryId, request.ParentCategoryId, parents))
        {
            throw new DomainException("Category parent would create a circular hierarchy.")
            {
                Code = CatalogErrorCodes.CircularCategory
            };
        }

        if (request.ParentCategoryId.HasValue &&
            CatalogPersistence.DepthOf(request.ParentCategoryId, parents) >= Category.MaxDepth)
        {
            throw new DomainException("Category hierarchy cannot exceed 5 levels.")
            {
                Code = CatalogErrorCodes.InvalidParent
            };
        }

        string slugSource = string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug;
        string slug = await CatalogPersistence.UniqueCategorySlugAsync(_db, slugSource, categoryId, cancellationToken);
        category.Update(request.Name, slug, request.Description, request.ParentCategoryId);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(category);
    }

    public async Task<CategoryResponse> ActivateAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        Category category = await LoadAsync(categoryId, cancellationToken);
        category.Activate(_clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(category);
    }

    public async Task<CategoryResponse> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        Category category = await LoadAsync(categoryId, cancellationToken);
        category.Deactivate(_clock.UtcNow);
        await CatalogPersistence.SaveChangesAsync(_db, cancellationToken);
        return CatalogMapping.ToResponse(category);
    }

    internal static IReadOnlyList<CategoryTreeResponse> BuildTree(IReadOnlyList<Category> categories, bool includeInactive)
    {
        IEnumerable<Category> source = includeInactive ? categories : categories.Where(c => c.IsActive);
        List<Category> list = source.ToList();
        Dictionary<Guid, List<Category>> byParent = list
            .GroupBy(c => c.ParentCategoryId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());

        return MapChildren(Guid.Empty, byParent);
    }

    private static List<CategoryTreeResponse> MapChildren(Guid parentId, Dictionary<Guid, List<Category>> byParent)
    {
        if (!byParent.TryGetValue(parentId, out List<Category>? children))
        {
            return [];
        }

        return children.Select(c => new CategoryTreeResponse(
            c.Id,
            c.Name,
            c.Slug,
            c.Description,
            c.IsActive,
            MapChildren(c.Id, byParent))).ToList();
    }

    private async Task EnsureParentAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null || parentId == Guid.Empty)
        {
            return;
        }

        Category? parent = await _db.Categories.FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken);
        if (parent is null)
        {
            throw new NotFoundException("Category", parentId);
        }
    }

    private async Task<Dictionary<Guid, Guid?>> LoadParentsAsync(CancellationToken cancellationToken)
    {
        return await _db.Categories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.ParentCategoryId, cancellationToken);
    }

    private async Task<Category> LoadAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
               ?? throw new NotFoundException("Category", categoryId);
    }
}
