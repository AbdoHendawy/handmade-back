using Handmade.Infrastructure.Identity.Email;
using Handmade.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class ProductionConfigFailFastTests
{
    private readonly HandmadeApiFactory _factory;

    public ProductionConfigFailFastTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Production_RejectsConsoleEmail()
    {
        using ProductionConfigFactory host = new(_factory, configure: builder =>
        {
            builder.UseSetting("Email:Provider", "Console");
            ApplyValidMinio(builder);
            builder.UseSetting("AllowedHosts", "localhost");
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("SMTP", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_RejectsMissingFileStorage()
    {
        using ProductionConfigFactory host = new(_factory, configure: builder =>
        {
            ApplyValidSmtp(builder);
            builder.UseSetting("FileStorage:Provider", string.Empty);
            builder.UseSetting("AllowedHosts", "localhost");
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("MinIO", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_RejectsWildcardAllowedHosts()
    {
        using ProductionConfigFactory host = new(_factory, configure: builder =>
        {
            ApplyValidSmtp(builder);
            ApplyValidMinio(builder);
            builder.UseSetting("AllowedHosts", "*");
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("AllowedHosts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyValidSmtp(IWebHostBuilder builder)
    {
        builder.UseSetting("Email:Provider", EmailOptions.SmtpProvider);
        builder.UseSetting("Email:Host", "smtp.test.local");
        builder.UseSetting("Email:Port", "587");
        builder.UseSetting("Email:FromAddress", "noreply@test.local");
    }

    private static void ApplyValidMinio(IWebHostBuilder builder)
    {
        builder.UseSetting("FileStorage:Provider", FileStorageOptions.MinioProvider);
        builder.UseSetting("FileStorage:Endpoint", "localhost:9000");
        builder.UseSetting("FileStorage:AccessKey", "test");
        builder.UseSetting("FileStorage:SecretKey", "testtest");
        builder.UseSetting("FileStorage:Bucket", "handmade");
        builder.UseSetting("FileStorage:PublicBaseUrl", "http://localhost:9000/handmade");
    }

    private sealed class ProductionConfigFactory : WebApplicationFactory<Program>
    {
        private readonly HandmadeApiFactory _inner;
        private readonly Action<IWebHostBuilder> _configure;

        public ProductionConfigFactory(HandmadeApiFactory inner, Action<IWebHostBuilder> configure)
        {
            _inner = inner;
            _configure = configure;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _inner.EnsureMigrated();
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("ConnectionStrings:Default", _inner.PostgresConnectionString);
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
            builder.UseSetting("Jwt:SecretKey", "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG!!");
            builder.UseSetting("Jwt:Issuer", "Handmade");
            builder.UseSetting("Jwt:Audience", "Handmade");
            builder.UseSetting("Hangfire:Enabled", "false");
            builder.UseSetting("AdminSeed:Enabled", "false");
            builder.UseSetting("RateLimiting:Enabled", "false");
            _configure(builder);
        }
    }
}
