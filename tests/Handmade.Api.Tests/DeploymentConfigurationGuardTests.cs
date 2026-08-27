using Handmade.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Tests;

public sealed class DeploymentConfigurationGuardTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void MissingConnectionString_Fails(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "",
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("Connection string", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", "Host=localhost;Port=5432;Database=handmade;Username=app;Password=prod-secret")]
    [InlineData("Staging", "Host=127.0.0.1;Port=5432;Database=handmade;Username=app;Password=prod-secret")]
    [InlineData("Production", "Host=::1;Port=5432;Database=handmade;Username=app;Password=prod-secret")]
    public void LocalhostConnectionString_Fails(string environmentName, string connectionString)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void KnownDevelopmentPassword_Fails(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=db.example.com;Port=5432;Database=handmade;Username=handmade;Password=handmade",
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("development database password", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=handmade", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", "Console")]
    [InlineData("Staging", "")]
    public void ConsoleOrEmptyEmail_Fails(string environmentName, string provider)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["Email:Provider"] = provider,
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("SMTP", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void SmtpMissingHost_FailsViaExistingValidation(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "SMTP",
            ["Email:Host"] = "",
            ["Email:Port"] = "587",
            ["Email:FromAddress"] = "noreply@example.com",
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("Email:Host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void EmptyFileStorage_Fails(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["FileStorage:Provider"] = "",
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("MinIO", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ValidMinio_PassesRelevantValidation(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>().WithValidStrictDefaults());
        DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void EmptyCors_Fails(string environmentName)
    {
        Dictionary<string, string?> values = new Dictionary<string, string?>().WithValidStrictDefaults();
        values.Remove("Cors:AllowedOrigins:0");

        IConfiguration config = BuildConfig(values);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("Cors", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", "*")]
    [InlineData("Staging", "")]
    public void WildcardOrEmptyAllowedHosts_Fails(string environmentName, string allowedHosts)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = allowedHosts,
        }.WithValidStrictDefaults());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName)));
        Assert.Contains("AllowedHosts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ExplicitAllowedHosts_Passes(string environmentName)
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "api.example.com",
        }.WithValidStrictDefaults());

        DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(environmentName));
    }

    [Fact]
    public void Development_AllowsConsoleEmailLocalDbAndWildcardHosts()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=localhost;Port=5432;Database=handmade;Username=handmade;Password=handmade",
            ["AllowedHosts"] = "*",
            ["Email:Provider"] = "Console",
            ["FileStorage:Provider"] = "",
            ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
        });

        DeploymentConfigurationGuard.Validate(config, new FakeHostEnvironment(Environments.Development));
    }

    [Fact]
    public void Development_IsNotStrict()
    {
        Assert.False(DeploymentConfigurationGuard.RequiresStrictConfig(new FakeHostEnvironment(Environments.Development)));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void StagingAndProduction_AreStrict(string environmentName)
    {
        Assert.True(DeploymentConfigurationGuard.RequiresStrictConfig(new FakeHostEnvironment(environmentName)));
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

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

internal static class DeploymentConfigurationGuardTestDefaults
{
    public static Dictionary<string, string?> WithValidStrictDefaults(this Dictionary<string, string?> values)
    {
        values.TryAdd("ConnectionStrings:Default",
            "Host=db.example.com;Port=5432;Database=handmade;Username=handmade;Password=prod-secret");
        values.TryAdd("AllowedHosts", "api.example.com");
        values.TryAdd("Cors:AllowedOrigins:0", "https://app.example.com");
        values.TryAdd("Email:Provider", "SMTP");
        values.TryAdd("Email:Host", "smtp.example.com");
        values.TryAdd("Email:Port", "587");
        values.TryAdd("Email:FromAddress", "noreply@example.com");
        values.TryAdd("FileStorage:Provider", "MinIO");
        values.TryAdd("FileStorage:Endpoint", "minio.example.com:9000");
        values.TryAdd("FileStorage:AccessKey", "access");
        values.TryAdd("FileStorage:SecretKey", "secret");
        values.TryAdd("FileStorage:Bucket", "handmade");
        values.TryAdd("FileStorage:PublicBaseUrl", "https://cdn.example.com/handmade");
        return values;
    }
}
