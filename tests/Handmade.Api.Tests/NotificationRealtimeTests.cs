using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications;
using Handmade.Application.Notifications.DTOs;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class NotificationRealtimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public NotificationRealtimeTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedHubConnection_IsRejected()
    {
        HubConnection connection = CreateHubConnection(accessToken: null);
        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        Assert.Contains("401", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAccessToken_IsAcceptedForNotificationHub()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);

        HttpRequestMessage negotiate = new(
            HttpMethod.Post,
            $"{NotificationHubRoutes.Notifications}/negotiate?negotiateVersion=1&access_token={user.AccessToken}");
        HttpResponseMessage response = await client.SendAsync(negotiate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConnectedUser_ReceivesDeliveryEvent_OnUserGroup()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        await using HubConnection connection = CreateHubConnection(user.AccessToken);
        TaskCompletionSource<JsonElement> received = BindReceived(connection);
        await connection.StartAsync();

        await CreateAdminNotificationAsync(client, user.User.Id, "Hello from hub");

        JsonElement payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("admin.broadcast", payload.GetProperty("type").GetString());
        Assert.Equal("Hello from hub", payload.GetProperty("message").GetString());
        Assert.False(payload.TryGetProperty("userId", out _));
        Assert.False(payload.TryGetProperty("deliveryStatus", out _));
    }

    [Fact]
    public async Task MultipleConnections_ForSameUser_AllReceive()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        await using HubConnection first = CreateHubConnection(user.AccessToken);
        await using HubConnection second = CreateHubConnection(user.AccessToken);
        TaskCompletionSource<JsonElement> firstReceived = BindReceived(first);
        TaskCompletionSource<JsonElement> secondReceived = BindReceived(second);
        await first.StartAsync();
        await second.StartAsync();

        await CreateAdminNotificationAsync(client, user.User.Id, "Fan-out");

        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task UserCannotReceiveAnotherUsersNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse userA = await RegisterAsync(client);
        AuthenticationResponse userB = await RegisterAsync(client);
        await using HubConnection connectionA = CreateHubConnection(userA.AccessToken);
        TaskCompletionSource<JsonElement> received = BindReceived(connectionA);
        await connectionA.StartAsync();

        await CreateAdminNotificationAsync(client, userB.User.Id, "Secret for B");

        TimeoutException timeout = await Assert.ThrowsAsync<TimeoutException>(
            () => received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.NotNull(timeout);
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOrigin_AndRejectsArbitraryOrigin()
    {
        HttpClient client = _factory.CreateMigratedClient();

        HttpResponseMessage allowed = await client.SendAsync(CorsPreflight("http://localhost:4200"));
        Assert.Equal("http://localhost:4200", GetHeader(allowed, "Access-Control-Allow-Origin"));
        Assert.Equal("true", GetHeader(allowed, "Access-Control-Allow-Credentials"));

        HttpResponseMessage denied = await client.SendAsync(CorsPreflight("https://evil.example"));
        Assert.NotEqual("https://evil.example", GetHeader(denied, "Access-Control-Allow-Origin"));
        Assert.NotEqual("*", GetHeader(denied, "Access-Control-Allow-Origin"));
    }

    private HubConnection CreateHubConnection(string? accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress!, NotificationHubRoutes.Notifications),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    }
                })
            .Build();
    }

    private async Task CreateAdminNotificationAsync(HttpClient client, Guid userId, string message)
    {
        AuthenticationResponse admin = await RegisterAsync(client);
        await _factory.AssignRoleAsync(admin.User.Id, RoleNames.Admin);
        AuthenticationResponse adminSession = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(admin.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/notifications",
            new AdminCreateNotificationRequest(
                "admin.broadcast",
                "Notice",
                message,
                userId,
                IdempotencyKey: $"hub:{userId:D}:{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static TaskCompletionSource<JsonElement> BindReceived(HubConnection connection)
    {
        TaskCompletionSource<JsonElement> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(NotificationHubMethods.NotificationReceived, payload => received.TrySetResult(payload));
        return received;
    }

    private static HttpRequestMessage CorsPreflight(string origin)
    {
        HttpRequestMessage request = new(HttpMethod.Options, NotificationHubRoutes.Notifications);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "Authorization");
        return request;
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        string email = $"hub_{Guid.NewGuid():N}@example.com";
        return (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
    }
}
