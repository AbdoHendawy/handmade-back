using Handmade.Api.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Tests;

public sealed class RateLimitingConfigurationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ValidDeploymentConfiguration_Accepted(string environmentName)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DisabledInDeployment_Rejected(string environmentName)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.Enabled = false;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName)));
        Assert.Contains("Enabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", 0)]
    [InlineData("Staging", -1)]
    public void ZeroOrNegativeAuthPermitLimit_Rejected(string environmentName, int permitLimit)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.Auth.PermitLimit = permitLimit;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName)));
        Assert.Contains("PermitLimit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", 0)]
    [InlineData("Staging", -5)]
    public void ZeroOrNegativeAuthWindowSeconds_Rejected(string environmentName, int windowSeconds)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.Auth.WindowSeconds = windowSeconds;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName)));
        Assert.Contains("WindowSeconds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ExcessiveAuthPermitLimit_Rejected(string environmentName)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.Auth.PermitLimit = RateLimitingOptions.MaxPermitLimit + 1;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName)));
        Assert.Contains("PermitLimit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void IntMaxValuePermitLimit_Rejected(string environmentName)
    {
        RateLimitingOptions options = ValidDeploymentOptions();
        options.Catalog.PermitLimit = int.MaxValue;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => options.EnsureValidForDeployment(new FakeHostEnvironment(environmentName)));
        Assert.Contains("PermitLimit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Development_DisabledWithZeroLimits_DoesNotThrowOnValidation()
    {
        RateLimitingOptions options = new()
        {
            Enabled = false,
            Auth = new RateLimitWindowOptions(),
            Catalog = new RateLimitWindowOptions()
        };

        options.EnsureValidForDeployment(new FakeHostEnvironment(Environments.Development));
    }

    [Fact]
    public void Development_ZeroLimits_ResolveToDefaults()
    {
        RateLimitingOptions options = new()
        {
            Enabled = true,
            Auth = new RateLimitWindowOptions(),
            Catalog = new RateLimitWindowOptions()
        };

        options.ApplyDevelopmentDefaults(new FakeHostEnvironment(Environments.Development));

        Assert.Equal(RateLimitingOptions.DefaultAuthPermitLimit, options.Auth.PermitLimit);
        Assert.Equal(RateLimitingOptions.DefaultAuthWindowSeconds, options.Auth.WindowSeconds);
        Assert.Equal(RateLimitingOptions.DefaultCatalogPermitLimit, options.Catalog.PermitLimit);
        Assert.Equal(RateLimitingOptions.DefaultCatalogWindowSeconds, options.Catalog.WindowSeconds);
    }

    private static RateLimitingOptions ValidDeploymentOptions()
    {
        return new RateLimitingOptions
        {
            Enabled = true,
            Auth = new RateLimitWindowOptions { PermitLimit = 20, WindowSeconds = 60 },
            Catalog = new RateLimitWindowOptions { PermitLimit = 120, WindowSeconds = 60 }
        };
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Handmade.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
