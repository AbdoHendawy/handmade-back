namespace Handmade.Application.Common;

/// <summary>
/// Shared application constants. Prefer strongly typed options for configuration values.
/// </summary>
public static class ApplicationConstants
{
    public const string ApiName = "Handmade API";

    public const string DefaultConnectionStringName = "Default";

    public const int JwtMinSecretLength = 32;
}

/// <summary>
/// Central route prefixes for URL versioning. Controllers compose module paths from these.
/// </summary>
public static class ApiRoutes
{
    public const string V1 = "api/v1";

    public const string Auth = $"{V1}/auth";

    public const string Admin = $"{V1}/admin";

    public const string Seller = $"{V1}/seller";

    public const string SellerApplications = $"{Seller}/applications";

    public const string SellerProfile = $"{Seller}/profile";

    public const string AdminSellerApplications = $"{Admin}/seller-applications";

    public const string AdminSellers = $"{Admin}/sellers";

    public const string Notifications = $"{V1}/notifications";

    public const string AdminNotifications = $"{Admin}/notifications";

    public const string Catalog = $"{V1}/catalog";

    public const string CatalogProducts = $"{Catalog}/products";

    public const string CatalogCategories = $"{Catalog}/categories";

    public const string SellerProducts = $"{Seller}/products";

    public const string AdminProducts = $"{Admin}/products";

    public const string AdminCategories = $"{Admin}/categories";

    public const string Status = $"{V1}/[controller]";
}
