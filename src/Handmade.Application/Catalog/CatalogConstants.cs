namespace Handmade.Application.Catalog;

public static class CatalogSortOptions
{
    public const string Newest = "newest";
    public const string PriceAsc = "priceAsc";
    public const string PriceDesc = "priceDesc";

    public static bool IsAllowed(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        string value = sort.Trim();
        return value.Equals(Newest, StringComparison.OrdinalIgnoreCase)
               || value.Equals(PriceAsc, StringComparison.OrdinalIgnoreCase)
               || value.Equals(PriceDesc, StringComparison.OrdinalIgnoreCase);
    }
}
