using System.Security.Cryptography;
using System.Text;

namespace Handmade.Application.Identity;

public static class RefreshTokenHashing
{
    public static string CreateOpaqueToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
