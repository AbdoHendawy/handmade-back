namespace Handmade.Application.Abstractions.Security;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessTokenResult GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        int securityStamp);
}
