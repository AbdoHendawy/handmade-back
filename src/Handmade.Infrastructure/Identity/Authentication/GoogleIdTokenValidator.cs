using Google.Apis.Auth;
using Handmade.Application.Abstractions.Authentication;
using Handmade.Application.Identity;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Handmade.Infrastructure.Identity.Authentication;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;
}

public sealed class GoogleIdTokenValidator : IExternalAuthProvider
{
    private readonly GoogleAuthOptions _options;

    public GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderName => AuthProviders.Google;

    public async Task<ExternalUserIdentity> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new DomainException("Google authentication is not configured.")
            {
                Code = AuthErrorCodes.GoogleNotConfigured
            };
        }

        try
        {
            GoogleJsonWebSignature.ValidationSettings settings = new()
            {
                Audience = [_options.ClientId]
            };

            GoogleJsonWebSignature.Payload payload =
                await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new DomainException("Google identity did not include an email.")
                {
                    Code = AuthErrorCodes.GoogleInvalidIdentity
                };
            }

            return new ExternalUserIdentity(
                AuthProviders.Google,
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.GivenName,
                payload.FamilyName);
        }
        catch (InvalidJwtException ex)
        {
            throw new DomainException("Invalid Google identity token.", ex)
            {
                Code = AuthErrorCodes.GoogleInvalidToken
            };
        }
    }
}
