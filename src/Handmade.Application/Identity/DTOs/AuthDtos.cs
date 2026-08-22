namespace Handmade.Application.Identity.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User);
