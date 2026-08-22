using System.Security.Cryptography;
using System.Text;
using Handmade.Application.Abstractions.Security;
using Konscious.Security.Cryptography;

namespace Handmade.Infrastructure.Identity.Security;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 8;
    private const int MemorySizeKb = 65536;
    private const int Iterations = 4;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Hash(password, salt);
        return $"argon2id${Iterations}${MemorySizeKb}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        string[] parts = passwordHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "argon2id")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int iterations) ||
            !int.TryParse(parts[2], out int memorySize))
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(parts[3]);
        byte[] expected = Convert.FromBase64String(parts[4]);
        byte[] actual = Hash(password, salt, iterations, memorySize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Hash(string password, byte[] salt, int iterations = Iterations, int memorySizeKb = MemorySizeKb)
    {
        using Argon2id argon2 = new(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = memorySizeKb,
            Iterations = iterations
        };

        return argon2.GetBytes(HashSize);
    }
}
