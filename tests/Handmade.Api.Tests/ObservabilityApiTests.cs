using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class ObservabilityApiTests
{
    private const string SentinelPassword = "SENTINEL_OBSERVABILITY_PASSWORD";
    private static readonly string[] SecretMarkers =
    [
        "Password=",
        "SecretKey",
        "Bearer ey",
        HandmadeApiFactory.SeededAdminPassword,
        "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG!!"
    ];

    private readonly HandmadeApiFactory _factory;

    public ObservabilityApiTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Factory_BootsWithObservabilityEnabled()
    {
        using HttpClient client = _factory.CreateMigratedClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Health_ReturnsHealthy_WithoutSecrets()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertResponseDoesNotContainSecretsAsync(response);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy_WithoutSecrets()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertResponseDoesNotContainSecretsAsync(response);
    }

    [Fact]
    public async Task Status_ReturnsOk()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NotFoundProblemDetails_ContainsNonEmptyTraceId()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog/products/non-existent-slug-xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string traceId = await ReadTraceIdAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(traceId));
    }

    [Fact]
    public async Task DifferentRequests_ReceiveDifferentTraceIds()
    {
        HttpClient client = _factory.CreateMigratedClient();

        HttpResponseMessage first = await client.GetAsync("/api/v1/catalog/products/non-existent-slug-a");
        HttpResponseMessage second = await client.GetAsync("/api/v1/catalog/products/non-existent-slug-b");

        string firstTraceId = await ReadTraceIdAsync(first);
        string secondTraceId = await ReadTraceIdAsync(second);

        Assert.False(string.IsNullOrWhiteSpace(firstTraceId));
        Assert.False(string.IsNullOrWhiteSpace(secondTraceId));
        Assert.NotEqual(firstTraceId, secondTraceId);
    }

    [Fact]
    public async Task AuthLoginFailure_DoesNotExposeSubmittedPassword()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = SentinelPassword });

        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SentinelPassword, body, StringComparison.Ordinal);
        foreach (string marker in SecretMarkers)
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<string> ReadTraceIdAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.GetProperty("traceId").GetString()
            ?? throw new InvalidOperationException("traceId was missing from ProblemDetails.");
    }

    private static async Task AssertResponseDoesNotContainSecretsAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        foreach (string marker in SecretMarkers)
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
