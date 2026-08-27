using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class RateLimitingApiTests
{
    private readonly HandmadeApiFactory _factory;

    public RateLimitingApiTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthLogin_ExceedingLimit_Returns429ProblemDetails()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 2, catalogPermitLimit: 100);
        using HttpClient client = limited.CreateClient();

        HttpResponseMessage? last = null;
        for (int i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "nobody@example.com", password = "not-used-here" });
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);

        await using Stream stream = await last.Content.ReadAsStreamAsync();
        using JsonDocument doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("rate_limited", doc.RootElement.GetProperty("code").GetString());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Catalog_ExceedingLimit_Returns429()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 100, catalogPermitLimit: 2);
        using HttpClient client = limited.CreateClient();

        HttpResponseMessage? last = null;
        for (int i = 0; i < 3; i++)
        {
            last = await client.GetAsync("/api/v1/catalog/categories");
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    private sealed class RateLimitedFactory : WebApplicationFactory<Program>
    {
        private readonly HandmadeApiFactory _inner;
        private readonly int _authPermitLimit;
        private readonly int _catalogPermitLimit;

        public RateLimitedFactory(HandmadeApiFactory inner, int authPermitLimit, int catalogPermitLimit)
        {
            _inner = inner;
            _authPermitLimit = authPermitLimit;
            _catalogPermitLimit = catalogPermitLimit;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _inner.EnsureMigrated();
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", _inner.PostgresConnectionString);
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
            builder.UseSetting("Jwt:SecretKey", "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG!!");
            builder.UseSetting("Jwt:Issuer", "Handmade");
            builder.UseSetting("Jwt:Audience", "Handmade");
            builder.UseSetting("Hangfire:Enabled", "false");
            builder.UseSetting("AdminSeed:Enabled", "false");
            builder.UseSetting("FileStorage:Provider", string.Empty);
            builder.UseSetting("Email:Provider", "Console");
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:Auth:PermitLimit", _authPermitLimit.ToString());
            builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
            builder.UseSetting("RateLimiting:Catalog:PermitLimit", _catalogPermitLimit.ToString());
            builder.UseSetting("RateLimiting:Catalog:WindowSeconds", "60");
        }
    }
}
