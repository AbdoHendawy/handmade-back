using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Identity;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? createdByIp)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        CreatedByIp = createdByIp;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? RevokedByIp { get; private set; }

    public User? User { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? createdByIp)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.") { Code = "invalid_refresh_token" };
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required.") { Code = "invalid_refresh_token" };
        }

        return new RefreshToken(CreateId(), userId, tokenHash, expiresAt, createdAt, createdByIp);
    }

    public void Revoke(DateTimeOffset revokedAt, string? revokedByIp, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = revokedAt;
        RevokedByIp = revokedByIp;
        ReplacedByTokenId = replacedByTokenId;
    }
}
