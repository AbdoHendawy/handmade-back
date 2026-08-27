using Handmade.Infrastructure.Persistence.Seeding;

namespace Handmade.Application.Tests;

public sealed class AdminSeedOptionsTests
{
    [Fact]
    public void Disabled_DoesNotRequireEmailOrPassword()
    {
        AdminSeedOptions options = new() { Enabled = false, Email = "", Password = "" };
        options.EnsureValidWhenEnabled();
    }

    [Fact]
    public void Enabled_MissingEmail_Fails()
    {
        AdminSeedOptions options = new()
        {
            Enabled = true,
            Email = "",
            Password = "DevOnly_Admin1!"
        };
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled);
    }

    [Fact]
    public void Enabled_WeakPassword_Fails()
    {
        AdminSeedOptions options = new()
        {
            Enabled = true,
            Email = "admin@localhost.local",
            Password = "short"
        };
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled);
    }

    [Fact]
    public void Enabled_ValidCredentials_Pass()
    {
        AdminSeedOptions options = new()
        {
            Enabled = true,
            Email = "admin@localhost.local",
            Password = "DevOnly_Admin1!"
        };
        options.EnsureValidWhenEnabled();
    }
}
