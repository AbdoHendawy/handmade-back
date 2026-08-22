namespace Handmade.Application.Identity;

/// <summary>
/// JWT / identity claim type names shared by token generation and validation.
/// </summary>
public static class AuthClaimTypes
{
    public const string Subject = "sub";

    public const string Email = "email";

    public const string SecurityStamp = "sst";
}

/// <summary>
/// Stable auth error codes returned via ProblemDetails.
/// </summary>
public static class AuthErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string InactiveAccount = "inactive_account";
    public const string GoogleNotConfigured = "google_not_configured";
    public const string GoogleEmailUnverified = "google_email_unverified";
    public const string GoogleInvalidIdentity = "google_invalid_identity";
    public const string GoogleInvalidToken = "google_invalid_token";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string RevokedRefreshToken = "revoked_refresh_token";
    public const string ExpiredRefreshToken = "expired_refresh_token";
    public const string RoleMissing = "role_missing";
}

/// <summary>
/// Shared field limits for Identity validation and persistence.
/// </summary>
public static class IdentityLimits
{
    public const int EmailMaxLength = 320;
    public const int NameMaxLength = 100;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int PasswordHashMaxLength = 500;
    public const int RoleNameMaxLength = 64;
    public const int ProviderMaxLength = 64;
    public const int ProviderUserIdMaxLength = 256;
    public const int RefreshTokenHashMaxLength = 128;
    public const int IpAddressMaxLength = 64;
}
