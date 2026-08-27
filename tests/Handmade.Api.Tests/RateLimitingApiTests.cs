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
    public void DefaultFactory_BootsWithRateLimitingDisabled()
    {
        using HttpClient client = _factory.CreateMigratedClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task AuthLogin_WithinLimit_Succeeds()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 5, catalogPermitLimit: 100);
        using HttpClient client = limited.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = "not-used-here" });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
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
    public async Task AuthLogin_ThrottledRequest_DoesNotExecuteHandler()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 2, catalogPermitLimit: 100);
        using HttpClient client = limited.CreateClient();

        for (int i = 0; i < 2; i++)
        {
            HttpResponseMessage withinLimit = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "nobody@example.com", password = "not-used-here" });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, withinLimit.StatusCode);
        }

        HttpResponseMessage throttled = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = "not-used-here" });

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, throttled.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadRequest, throttled.StatusCode);
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

    [Fact]
    public async Task Catalog_AfterWindowReset_AcceptsRequestAgain()
    {
        await using RateLimitedFactory limited = new(
            _factory,
            authPermitLimit: 100,
            catalogPermitLimit: 2,
            catalogWindowSeconds: 1);
        using HttpClient client = limited.CreateClient();

        for (int i = 0; i < 3; i++)
        {
            await client.GetAsync("/api/v1/catalog/categories");
        }

        HttpResponseMessage throttled = await client.GetAsync("/api/v1/catalog/categories");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        await Task.Delay(1100);

        HttpResponseMessage afterReset = await client.GetAsync("/api/v1/catalog/categories");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, afterReset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterReset.StatusCode);
    }

    [Fact]
    public async Task NonRateLimitedHealthEndpoint_RemainsUsable()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 1, catalogPermitLimit: 1);
        using HttpClient client = limited.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithoutToken_StillReturns401WhenNotThrottled()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 100, catalogPermitLimit: 100);
        using HttpClient client = limited.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_ConcurrentRequests_RespectPermitLimit()
    {
        await using RateLimitedFactory limited = new(_factory, authPermitLimit: 100, catalogPermitLimit: 3);
        using HttpClient client = limited.CreateClient();

        Task<HttpResponseMessage>[] requests = Enumerable.Range(0, 8)
            .Select(_ => client.GetAsync("/api/v1/catalog/categories"))
            .ToArray();

        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        int accepted = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        int throttled = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.InRange(accepted, 1, 3);
        Assert.True(throttled >= 5);
    }

    private sealed class RateLimitedFactory : WebApplicationFactory<Program>
    {
        private readonly HandmadeApiFactory _inner;
        private readonly int _authPermitLimit;
        private readonly int _catalogPermitLimit;
        private readonly int _authWindowSeconds;
        private readonly int _catalogWindowSeconds;

        public RateLimitedFactory(
            HandmadeApiFactory inner,
            int authPermitLimit,
            int catalogPermitLimit,
            int authWindowSeconds = 60,
            int catalogWindowSeconds = 60)
        {
            _inner = inner;
            _authPermitLimit = authPermitLimit;
            _catalogPermitLimit = catalogPermitLimit;
            _authWindowSeconds = authWindowSeconds;
            _catalogWindowSeconds = catalogWindowSeconds;
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
            builder.UseSetting("RateLimiting:Auth:WindowSeconds", _authWindowSeconds.ToString());
            builder.UseSetting("RateLimiting:Catalog:PermitLimit", _catalogPermitLimit.ToString());
            builder.UseSetting("RateLimiting:Catalog:WindowSeconds", _catalogWindowSeconds.ToString());
        }
    }
}
