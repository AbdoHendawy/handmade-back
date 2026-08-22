using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class NotificationEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public NotificationEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Inbox_RequiresAuthentication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Apply_PersistsInAppNotification_AndSupportsReadUnread()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Abdo Handmade",
                "Handmade accessories and crafts studio.",
                "+201000000001"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        UnreadCountResponse unread = (await (await client.GetAsync("/api/v1/notifications/unread-count")).Content
            .ReadFromJsonAsync<UnreadCountResponse>(JsonOptions))!;
        Assert.Equal(1, unread.Count);

        PagedResult<NotificationResponse> page = (await (await client.GetAsync("/api/v1/notifications?unreadOnly=true")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Assert.Equal(1, page.TotalCount);
        NotificationResponse item = page.Items[0];
        Assert.Equal(NotificationTypes.SellerApplicationSubmitted, item.Type);
        Assert.False(item.IsRead);
        Assert.Equal("Delivered", item.DeliveryStatus);

        HttpResponseMessage marked = await client.PostAsync($"/api/v1/notifications/{item.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        UnreadCountResponse after = (await (await client.GetAsync("/api/v1/notifications/unread-count")).Content
            .ReadFromJsonAsync<UnreadCountResponse>(JsonOptions))!;
        Assert.Equal(0, after.Count);
    }

    [Fact]
    public async Task CannotReadAnotherUsersNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse owner = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Abdo Handmade",
                "Handmade accessories and crafts studio.",
                "+201000000001"));

        PagedResult<NotificationResponse> page = (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Guid notificationId = page.Items[0].Id;

        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        HttpResponseMessage response = await client.PostAsync($"/api/v1/notifications/{notificationId}/read", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_CreatesSellerApprovedNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse application = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Abdo Handmade",
                "Handmade accessories and crafts studio.",
                "+201000000001"))).Content
            .ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;

        AuthenticationResponse admin = await RegisterAsync(client);
        await _factory.AssignRoleAsync(admin.User.Id, RoleNames.Admin);
        AuthenticationResponse adminSession = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(admin.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        Authorize(client, adminSession.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", content: null)).StatusCode);

        Authorize(client, applicant.AccessToken);
        PagedResult<NotificationResponse> page = (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Assert.Contains(page.Items, n => n.Type == NotificationTypes.SellerApplicationApproved);

        HttpResponseMessage readAll = await client.PostAsync("/api/v1/notifications/read-all", content: null);
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        UnreadCountResponse unread = (await (await client.GetAsync("/api/v1/notifications/unread-count")).Content
            .ReadFromJsonAsync<UnreadCountResponse>(JsonOptions))!;
        Assert.Equal(0, unread.Count);
    }

    [Fact]
    public async Task User_CanCreateGetUpdateAndDeleteOwnNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/notifications",
            new CreateInboxNotificationRequest("system.manual", "Hello", "Please review your shop.", null, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        NotificationResponse item = (await created.Content.ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(user.User.Id, item.UserId);
        Assert.Equal("Hello", item.Title);

        NotificationResponse fetched = (await (await client.GetAsync($"/api/v1/notifications/{item.Id}")).Content
            .ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(item.Id, fetched.Id);

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"/api/v1/notifications/{item.Id}",
            new UpdateNotificationRequest("Updated", "New body", true, null));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        NotificationResponse afterUpdate = (await updated.Content.ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal("Updated", afterUpdate.Title);
        Assert.True(afterUpdate.IsRead);

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/v1/notifications/{item.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/notifications/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task CannotUpdateOrDeleteAnotherUsersNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse owner = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        NotificationResponse item = (await (await client.PostAsJsonAsync(
            "/api/v1/notifications",
            new CreateInboxNotificationRequest("system.manual", "Hello", "Body", null, null))).Content
            .ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;

        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/notifications/{item.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/api/v1/notifications/{item.Id}",
                new UpdateNotificationRequest("Hacked", "x", true, null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/v1/notifications/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateForUser_AndCustomerCannotUseAdminApi()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse target = await RegisterAsync(client);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                "/api/v1/admin/notifications",
                new AdminCreateNotificationRequest(
                    "admin.broadcast",
                    "Notice",
                    "Hello",
                    target.User.Id))).StatusCode);

        AuthenticationResponse admin = await RegisterAsync(client);
        await _factory.AssignRoleAsync(admin.User.Id, RoleNames.Admin);
        AuthenticationResponse adminSession = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(admin.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        Authorize(client, adminSession.AccessToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/notifications",
            new AdminCreateNotificationRequest(
                "admin.broadcast",
                "Notice",
                "Hello from admin",
                target.User.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        NotificationResponse item = (await created.Content.ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(target.User.Id, item.UserId);

        PagedResult<NotificationResponse> adminList = (await (await client.GetAsync(
            $"/api/v1/admin/notifications?userId={target.User.Id}")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Assert.Contains(adminList.Items, n => n.Id == item.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/notifications/{item.Id}")).StatusCode);
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        string email = $"notify_{Guid.NewGuid():N}@example.com";
        return (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
