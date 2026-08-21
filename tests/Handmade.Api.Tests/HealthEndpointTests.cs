using System.Net;
using System.Net.Http.Json;
using Handmade.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Handmade.Api.Tests;

public sealed class HandmadeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("handmade_test")
        .WithUsername("handmade")
        .WithPassword("handmade")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<HandmadeApiFactory>;

[Collection(nameof(ApiCollection))]
public sealed class HealthEndpointTests
{
    private readonly HandmadeApiFactory _factory;

    public HealthEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy_WhenPostgresIsAvailable()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Status_ReturnsOk()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Dictionary<string, object>? payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(payload);
        Assert.Equal("v1", payload!["version"]?.ToString());
    }

    [Fact]
    public async Task OpenApi_IsAvailable_InDevelopment()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

[Collection(nameof(ApiCollection))]
public sealed class DatabaseConnectivityTests
{
    private readonly HandmadeApiFactory _factory;

    public DatabaseConnectivityTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DbContext_CanConnect()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext dbContext = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();

        bool canConnect = await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }
}
