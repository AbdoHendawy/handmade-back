namespace Handmade.Application.Abstractions.Authentication;

public sealed record ExternalUserIdentity(
    string Provider,
    string ProviderUserId,
    string Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName);

public interface IExternalAuthProvider
{
    string ProviderName { get; }

    Task<ExternalUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
