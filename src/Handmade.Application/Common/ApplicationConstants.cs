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

    public const string Status = $"{V1}/[controller]";
}
