using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Register_CreatesWelcomeInAppNotification_WithoutDuplicatingWelcomeEmail()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        PagedResult<NotificationResponse> page = await ListAsync(client);
        NotificationResponse welcome = Assert.Single(page.Items, n => n.Type == NotificationTypes.Welcome);
        Assert.Equal(user.User.Id, welcome.UserId);
        Assert.Contains($"\"userId\":\"{user.User.Id:D}\"", welcome.DataJson, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            1,
            _factory.Emails.Sent.Count(m => m.To == user.User.Email && m.Subject == "Welcome to Handmade"));
    }

    [Fact]
    public async Task Clients_CannotCreateArbitraryNotifications()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/notifications",
            new { type = "system.manual", title = "Hello", body = "Nope" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, created.StatusCode);

        HttpResponseMessage anonymous = await _factory.CreateMigratedClient()
            .PostAsJsonAsync("/api/v1/notifications", new { type = "system.manual", title = "Hello", body = "Nope" });
        Assert.True(
            anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.MethodNotAllowed,
            anonymous.StatusCode.ToString());
    }

    [Fact]
    public async Task QueryStringAccessToken_IsRejectedForRestApi()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/notifications?access_token={user.AccessToken}");
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

        PagedResult<NotificationResponse> page = await ListAsync(client, unreadOnly: true);
        NotificationResponse item = Assert.Single(page.Items, n => n.Type == NotificationTypes.SellerApplicationSubmitted);
        Assert.False(item.IsRead);
        Assert.Equal("Delivered", item.DeliveryStatus);
        NotificationResponse fetched = (await (await client.GetAsync($"/api/v1/notifications/{item.Id}")).Content
            .ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(item.Id, fetched.Id);
        Assert.Contains("applicationId", fetched.DataJson, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage marked = await client.PostAsync($"/api/v1/notifications/{item.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        PagedResult<NotificationResponse> unread = await ListAsync(client, unreadOnly: true);
        Assert.DoesNotContain(unread.Items, n => n.Id == item.Id);
    }

    [Fact]
    public async Task CannotReadAnotherUsersNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse owner = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        PagedResult<NotificationResponse> page = await ListAsync(client);
        Guid notificationId = page.Items[0].Id;

        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/notifications/{notificationId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/notifications/{notificationId}/read", content: null)).StatusCode);
    }

    [Fact]
    public async Task Approve_CreatesSellerApprovedNotification()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (AuthenticationResponse applicant, SellerApplicationResponse application) = await SubmitApplicationAsync(client);

        await LoginAsAdminAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", content: null)).StatusCode);

        Authorize(client, applicant.AccessToken);
        PagedResult<NotificationResponse> page = await ListAsync(client);
        NotificationResponse approved = Assert.Single(page.Items, n => n.Type == NotificationTypes.SellerApplicationApproved);
        Assert.Contains("sellerId", approved.DataJson, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage readAll = await client.PostAsync("/api/v1/notifications/read-all", content: null);
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        UnreadCountResponse unread = (await (await client.GetAsync("/api/v1/notifications/unread-count")).Content
            .ReadFromJsonAsync<UnreadCountResponse>(JsonOptions))!;
        Assert.Equal(0, unread.Count);
    }

    [Fact]
    public async Task Reject_IncludesUserFacingReason()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (AuthenticationResponse applicant, SellerApplicationResponse application) = await SubmitApplicationAsync(client);

        const string reason = "Please provide a more detailed business description.";
        await LoginAsAdminAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/seller-applications/{application.Id}/reject",
                new RejectSellerApplicationRequest(reason))).StatusCode);

        Authorize(client, applicant.AccessToken);
        NotificationResponse rejected = Assert.Single(
            (await ListAsync(client)).Items,
            n => n.Type == NotificationTypes.SellerApplicationRejected);
        Assert.Contains(reason, rejected.Body);
        Assert.Contains(reason, rejected.DataJson);
        Assert.DoesNotContain("rejectedBy", rejected.DataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuspendAndReactivate_CreateSellerNotifications()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (AuthenticationResponse applicant, SellerApplicationResponse application) = await SubmitApplicationAsync(client);
        await LoginAsAdminAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", content: null)).StatusCode);

        Authorize(client, applicant.AccessToken);
        SellerProfileResponse profile = (await (await client.GetAsync("/api/v1/seller/profile")).Content
            .ReadFromJsonAsync<SellerProfileResponse>(JsonOptions))!;

        await LoginAsAdminAsync(client);
        const string reason = "Policy violation documented for the seller.";
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{profile.Id}/suspend",
                new SuspendSellerRequest(reason))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/sellers/{profile.Id}/reactivate", content: null)).StatusCode);

        Authorize(client, applicant.AccessToken);
        PagedResult<NotificationResponse> page = await ListAsync(client);
        NotificationResponse suspended = Assert.Single(page.Items, n => n.Type == NotificationTypes.SellerSuspended);
        Assert.Contains(reason, suspended.Body);
        Assert.Contains($"\"sellerId\":\"{profile.Id:D}\"", suspended.DataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(page.Items, n => n.Type == NotificationTypes.SellerReactivated);
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

        await LoginAsAdminAsync(client);

        string idempotencyKey = $"admin.broadcast:{Guid.NewGuid():N}";
        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/notifications",
            new AdminCreateNotificationRequest(
                "admin.broadcast",
                "Notice",
                "Hello from admin",
                target.User.Id,
                IdempotencyKey: idempotencyKey));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        NotificationResponse item = (await created.Content.ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(target.User.Id, item.UserId);

        HttpResponseMessage duplicate = await client.PostAsJsonAsync(
            "/api/v1/admin/notifications",
            new AdminCreateNotificationRequest(
                "admin.broadcast",
                "Notice again",
                "Should not duplicate",
                target.User.Id,
                IdempotencyKey: idempotencyKey));
        NotificationResponse same = (await duplicate.Content.ReadFromJsonAsync<NotificationResponse>(JsonOptions))!;
        Assert.Equal(item.Id, same.Id);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            INotificationDeliveryService delivery = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();
            await delivery.DeliverAsync(item.Id);
            await delivery.DeliverAsync(item.Id);
        }

        Authorize(client, target.AccessToken);
        Assert.Equal(1, (await ListAsync(client)).Items.Count(n => n.Id == item.Id));

        await LoginAsAdminAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/notifications/{item.Id}")).StatusCode);
    }

    private async Task<(AuthenticationResponse Applicant, SellerApplicationResponse Application)> SubmitApplicationAsync(
        HttpClient client)
    {
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse application = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Abdo Handmade",
                "Handmade accessories and crafts studio.",
                "+201000000001"))).Content
            .ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
        return (applicant, application);
    }

    private async Task LoginAsAdminAsync(HttpClient client)
    {
        AuthenticationResponse admin = await RegisterAsync(client);
        await _factory.AssignRoleAsync(admin.User.Id, RoleNames.Admin);
        AuthenticationResponse adminSession = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(admin.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        Authorize(client, adminSession.AccessToken);
    }

    private static async Task<PagedResult<NotificationResponse>> ListAsync(HttpClient client, bool unreadOnly = false)
    {
        string suffix = unreadOnly ? "?unreadOnly=true" : string.Empty;
        return (await (await client.GetAsync($"/api/v1/notifications{suffix}")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
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
