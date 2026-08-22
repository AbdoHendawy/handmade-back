using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

public sealed class ProductImage : Entity, IAuditable
{
    public const int StorageKeyMaxLength = 500;
    public const int UrlMaxLength = 1000;

    private ProductImage()
    {
    }

    internal ProductImage(Guid id, Guid productId, string storageKey, string url, int sortOrder, bool isPrimary)
        : base(id)
    {
        ProductId = productId;
        StorageKey = storageKey;
        Url = url;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }

    public Guid ProductId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    public void ClearPrimary() => IsPrimary = false;

    public void MarkPrimary() => IsPrimary = true;

    public void SetSortOrder(int sortOrder)
    {
        if (sortOrder < 1)
        {
            throw new DomainException("Sort order must be at least 1.") { Code = CatalogErrorCodes.InvalidSortOrder };
        }

        SortOrder = sortOrder;
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProductImage Create(Guid productId, string storageKey, string? url, int sortOrder, bool isPrimary)
    {
        if (sortOrder < 1)
        {
            throw new DomainException("Sort order must be at least 1.") { Code = CatalogErrorCodes.InvalidSortOrder };
        }

        string key = RequireStorageKey(storageKey);
        return new ProductImage(CreateId(), productId, key, RequireUrl(url, key), sortOrder, isPrimary);
    }

    public static string RequireStorageKey(string storageKey)
    {
        string trimmed = storageKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > StorageKeyMaxLength)
        {
            throw new DomainException("Image storage key is required.") { Code = CatalogErrorCodes.InvalidStorageKey };
        }

        return trimmed;
    }

    public static string RequireUrl(string? url, string storageKey)
    {
        string trimmed = url?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = $"storage://{storageKey}";
        }

        if (trimmed.Length > UrlMaxLength)
        {
            throw new DomainException("Image URL is too long.") { Code = CatalogErrorCodes.InvalidStorageKey };
        }

        return trimmed;
    }
}
