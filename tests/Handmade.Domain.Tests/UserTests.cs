using Handmade.Domain.Identity;
using Handmade.Domain.Identity.Events;

namespace Handmade.Domain.Tests;

public sealed class UserTests
{
    [Fact]
    public void RegisterLocal_NormalizesEmail_AndRaisesEvent()
    {
        User user = User.RegisterLocal("Test@Example.COM", "hash", "Abdo", "Hendawy");

        Assert.Equal("test@example.com", user.Email);
        Assert.Contains(user.DomainEvents, e => e is UserRegisteredEvent);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public void RegisterExternal_MarksEmailVerified_WhenRequested()
    {
        User user = User.RegisterExternal("a@b.com", "A", "B", isEmailVerified: true);
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public void IncrementSecurityStamp_ChangesValue()
    {
        User user = User.RegisterLocal("a@b.com", "hash", "A", "B");
        int before = user.SecurityStamp;
        user.IncrementSecurityStamp();
        Assert.Equal(before + 1, user.SecurityStamp);
    }
}
