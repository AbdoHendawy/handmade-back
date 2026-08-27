using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

public sealed class ProductVariant : Entity, IAuditable
{
    public const int NameMaxLength = 120;
    public const int SkuMaxLength = 64;

    private ProductVariant()
    {
    }

    internal ProductVariant(Guid id, Guid productId, string name, string sku, decimal price, string currency)
        : base(id)
    {
        ProductId = productId;
        Name = name;
        Sku = sku;
        Price = price;
        Currency = currency;
    }

    public Guid ProductId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = CatalogMoney.DefaultCurrency;

    public int StockQuantity { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProductVariant Create(Guid productId, string name, string sku, decimal price, string currency)
    {
        return new ProductVariant(
            CreateId(),
            productId,
            RequireName(name),
            RequireSku(sku),
            CatalogMoney.RequireAmount(price),
            CatalogMoney.RequireCurrency(currency));
    }

    public void Update(string name, string sku, decimal price, string currency)
    {
        Name = RequireName(name);
        Sku = RequireSku(sku);
        Price = CatalogMoney.RequireAmount(price);
        Currency = CatalogMoney.RequireCurrency(currency);
    }

    public void SetStock(int quantity)
    {
        StockQuantity = Product.RequireNonNegativeStock(quantity);
    }

    public void DecrementStock(int quantity)
    {
        StockQuantity = Product.ApplyDecrement(StockQuantity, quantity);
    }

    public void IncrementStock(int quantity)
    {
        StockQuantity = Product.ApplyIncrement(StockQuantity, quantity);
    }

    public static string RequireName(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
        {
            throw new DomainException("Variant name is required.") { Code = CatalogErrorCodes.InvalidName };
        }

        return trimmed;
    }

    public static string RequireSku(string sku)
    {
        string trimmed = sku?.Trim().ToUpperInvariant() ?? string.Empty;
        if (trimmed.Length is < 1 or > SkuMaxLength || trimmed.Contains(' '))
        {
            throw new DomainException("SKU is required and cannot contain spaces.") { Code = CatalogErrorCodes.InvalidSku };
        }

        return trimmed;
    }
}
