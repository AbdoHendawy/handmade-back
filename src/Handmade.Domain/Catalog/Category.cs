using Handmade.Domain.Common;
using Handmade.Domain.Catalog.Events;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

public sealed class Category : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 80;
    public const int SlugMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int MaxDepth = 5;

    private Category()
    {
    }

    private Category(Guid id, string name, string slug, string? description, Guid? parentCategoryId)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        ParentCategoryId = parentCategoryId;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Category Create(string name, string slug, string? description, Guid? parentCategoryId, DateTimeOffset now)
    {
        Category category = new(
            CreateId(),
            RequireName(name),
            CatalogSlug.Require(slug),
            RequireDescription(description),
            parentCategoryId == Guid.Empty ? null : parentCategoryId);

        category.Raise(new CategoryCreated(category.Id, category.Slug, now));
        return category;
    }

    public void Update(string name, string slug, string? description, Guid? parentCategoryId)
    {
        Guid? parent = parentCategoryId == Guid.Empty ? null : parentCategoryId;
        if (parent == Id)
        {
            throw new DomainException("A category cannot be its own parent.") { Code = CatalogErrorCodes.InvalidParent };
        }

        Name = RequireName(name);
        Slug = CatalogSlug.Require(slug);
        Description = RequireDescription(description);
        ParentCategoryId = parent;
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Raise(new CategoryActivated(Id, now));
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Raise(new CategoryDeactivated(Id, now));
    }

    public static string RequireName(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 2 or > NameMaxLength)
        {
            throw new DomainException("Category name must be between 2 and 80 characters.")
            {
                Code = CatalogErrorCodes.InvalidName
            };
        }

        return trimmed;
    }

    public static string? RequireDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string trimmed = description.Trim();
        if (trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainException("Category description is too long.") { Code = CatalogErrorCodes.InvalidDescription };
        }

        return trimmed;
    }
}
