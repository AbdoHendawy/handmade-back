using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Identity.DTOs;
using Handmade.Domain.Identity;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

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
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy_WhenPostgresIsAvailable()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Status_ReturnsOk()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_IsAvailable_InDevelopment()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bearer", json, StringComparison.Ordinal);
        Assert.Contains("securitySchemes", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerUi_IsAvailable_InDevelopment()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string html = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DbContext_CanConnect()
    {
        _factory.CreateMigratedClient();
        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext dbContext = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }
}

[Collection(nameof(ApiCollection))]
public sealed class AuthEndpointTests
{
    private readonly HandmadeApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_AssignsCustomer_AndReturnsTokens()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"user_{Guid.NewGuid():N}@example.com";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            email,
            "StrongPass1!",
            "Abdo",
            "Hendawy"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthenticationResponse? body = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Contains(RoleNames.Customer, body.User.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"dup_{Guid.NewGuid():N}@example.com";
        RegisterRequest request = new(email, "StrongPass1!", "Abdo", "Hendawy");

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/register", request)).StatusCode);
        HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Works()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"login_{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass1!", "A", "B"));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "StrongPass1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsBadRequest()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"bad_{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass1!", "A", "B"));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "WrongPass1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldCannotReuse()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"refresh_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse registered = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "A", "B"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        HttpResponseMessage refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(registered.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        AuthenticationResponse refreshed = (await refreshResponse.Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        HttpResponseMessage reuse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(registered.RefreshToken));
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        HttpClient client = _factory.CreateMigratedClient();
        string email = $"logout_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse registered = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "A", "B"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        HttpResponseMessage logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest(registered.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        HttpResponseMessage refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(registered.RefreshToken));
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_RequiresAuth_AndReturnsProfile()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        string email = $"me_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse registered = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);
        HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        UserResponse? profile = await me.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(email, profile!.Email);
    }

    [Fact]
    public async Task AdminPing_EnforcesRoles()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/ping")).StatusCode);

        string email = $"cust_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse customer = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "A", "B"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/ping")).StatusCode);

        await _factory.AssignRoleAsync(customer.User.Id, RoleNames.Admin);

        // New login to get roles in token
        client.DefaultRequestHeaders.Authorization = null;
        AuthenticationResponse adminLogin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/ping")).StatusCode);
    }

    [Fact]
    public async Task AdminRevokeSessions_InvalidatesAccessToken()
    {
        HttpClient client = _factory.CreateMigratedClient();

        string adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse adminReg = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(adminEmail, "StrongPass1!", "Admin", "User"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        await _factory.AssignRoleAsync(adminReg.User.Id, RoleNames.Admin);
        AuthenticationResponse admin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(adminEmail, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        string victimEmail = $"victim_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse victim = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(victimEmail, "StrongPass1!", "V", "U"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        HttpResponseMessage revoke = await client.PostAsync(
            $"/api/v1/admin/users/{victim.User.Id}/revoke-sessions",
            null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", victim.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
