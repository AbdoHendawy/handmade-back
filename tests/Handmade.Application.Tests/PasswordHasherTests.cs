using Handmade.Infrastructure.Identity.Security;

namespace Handmade.Application.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashAndVerify_RoundTrips()
    {
        Argon2PasswordHasher hasher = new();
        string hash = hasher.HashPassword("StrongPass1!");
        Assert.True(hasher.VerifyPassword("StrongPass1!", hash));
        Assert.False(hasher.VerifyPassword("WrongPass1!", hash));
    }
}
