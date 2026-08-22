using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Identity;

public sealed class ExternalLogin : Entity
{
    private ExternalLogin()
    {
    }

    private ExternalLogin(
        Guid id,
        Guid userId,
        string provider,
        string providerUserId,
        string? providerEmail,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
        ProviderEmail = providerEmail;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderUserId { get; private set; } = string.Empty;

    public string? ProviderEmail { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public User? User { get; private set; }

    public static ExternalLogin Create(
        Guid userId,
        string provider,
        string providerUserId,
        string? providerEmail,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.") { Code = "invalid_external_login" };
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainException("Provider is required.") { Code = "invalid_external_login" };
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new DomainException("Provider user id is required.") { Code = "invalid_external_login" };
        }

        return new ExternalLogin(
            CreateId(),
            userId,
            provider.Trim(),
            providerUserId.Trim(),
            string.IsNullOrWhiteSpace(providerEmail) ? null : providerEmail.Trim().ToLowerInvariant(),
            createdAt);
    }
}
