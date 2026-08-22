using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Handmade.Domain.Seller;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class SellerEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public SellerEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Apply_RequiresAuthentication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            ValidSubmit());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUser_CanApply_AndPendingCannotApplyAgain()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);

        Authorize(client, user.AccessToken);
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/v1/seller/applications", ValidSubmit());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        SellerApplicationResponse application = (await created.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
        Assert.Equal(nameof(SellerApplicationStatus.Pending), application.Status);
        Assert.Equal(user.User.Id, application.UserId);

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/seller/applications", ValidSubmit("Other Shop Name"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        Assert.Contains(
            _factory.Emails.Sent,
            m => m.To == user.User.Email && m.Subject == "Your Seller Application Was Received");
    }

    [Fact]
    public async Task MassAssignment_CannotSetStatusOrUserId()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        using StringContent body = new(
            """
            {
              "businessName": "Abdo Handmade",
              "description": "Handmade accessories and crafts studio.",
              "phone": "+201000000001",
              "status": "Approved",
              "userId": "00000000-0000-0000-0000-000000000099",
              "reviewedBy": "00000000-0000-0000-0000-000000000098"
            }
            """,
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/v1/seller/applications", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        SellerApplicationResponse application = (await response.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
        Assert.Equal(nameof(SellerApplicationStatus.Pending), application.Status);
        Assert.Equal(user.User.Id, application.UserId);
        Assert.Null(application.ReviewedBy);
    }

    [Fact]
    public async Task RejectedUser_CanReapply_ApprovedSellerCannot()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (AuthenticationResponse admin, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);

        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        Authorize(client, adminToken);
        HttpResponseMessage rejected = await client.PostAsJsonAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/reject",
            new RejectSellerApplicationRequest("Please provide a more detailed business description."));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        Authorize(client, applicant.AccessToken);
        HttpResponseMessage reapply = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            ValidSubmit("Second Studio Name"));
        Assert.Equal(HttpStatusCode.Created, reapply.StatusCode);

        SellerApplicationResponse second = (await reapply.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
        Authorize(client, adminToken);
        HttpResponseMessage approved = await client.PostAsync(
            $"/api/v1/admin/seller-applications/{second.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        Authorize(client, applicant.AccessToken);
        HttpResponseMessage afterApproval = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            ValidSubmit("Third Studio Name"));
        Assert.Equal(HttpStatusCode.Conflict, afterApproval.StatusCode);

        Assert.Contains(
            _factory.Emails.Sent,
            m => m.To == applicant.User.Email && m.Subject == "Update About Your Seller Application");
        Assert.Contains(
            _factory.Emails.Sent,
            m => m.To == applicant.User.Email && m.Subject == "Congratulations! Your Seller Account Is Approved");

        _ = admin;
    }

    [Fact]
    public async Task CustomerAndSeller_CannotApproveOrReject()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        HttpResponseMessage customerApprove = await client.PostAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, customerApprove.StatusCode);

        HttpResponseMessage customerReject = await client.PostAsJsonAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/reject",
            new RejectSellerApplicationRequest("Please provide a more detailed business description."));
        Assert.Equal(HttpStatusCode.Forbidden, customerReject.StatusCode);

        (_, string adminToken) = await CreateAdminAsync(client);
        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{pending.Id}/approve", null)).StatusCode);

        AuthenticationResponse sellerLogin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(applicant.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        Authorize(client, sellerLogin.AccessToken);
        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        SellerApplicationResponse otherPending = await SubmitAsync(client);

        Authorize(client, sellerLogin.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{otherPending.Id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task CannotApproveRejectedOrApprovedApplication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/seller-applications/{pending.Id}/reject",
                new RejectSellerApplicationRequest("Please provide a more detailed business description."))).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{pending.Id}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/seller-applications/{pending.Id}/reject",
                new RejectSellerApplicationRequest("Please provide a more detailed business description."))).StatusCode);
    }

    [Fact]
    public async Task AdminCannotApproveOwnApplication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (AuthenticationResponse admin, string adminToken) = await CreateAdminAsync(client);
        Authorize(client, adminToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/approve",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _ = admin;
    }

    [Fact]
    public async Task Approval_CreatesProfile_AssignsRole_AndProfileOwnershipIsEnforced()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{pending.Id}/approve", null)).StatusCode);

        Authorize(client, applicant.AccessToken);
        HttpResponseMessage profileResponse = await client.GetAsync("/api/v1/seller/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        SellerProfileResponse profile = (await profileResponse.Content.ReadFromJsonAsync<SellerProfileResponse>(JsonOptions))!;
        Assert.Equal(nameof(SellerProfileStatus.Active), profile.Status);
        Assert.Equal(applicant.User.Id, profile.UserId);

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            "/api/v1/seller/profile",
            new UpdateSellerProfileRequest(
                "Updated Studio",
                "Updated description for the handmade studio.",
                "+201000000099"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        SellerProfileResponse updatedProfile = (await updated.Content.ReadFromJsonAsync<SellerProfileResponse>(JsonOptions))!;
        Assert.Equal("Updated Studio", updatedProfile.BusinessName);
        Assert.Equal(nameof(SellerProfileStatus.Active), updatedProfile.Status);
        Assert.Equal(applicant.User.Id, updatedProfile.UserId);

        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/seller/profile")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                "/api/v1/seller/profile",
                new UpdateSellerProfileRequest(
                    "Hijack",
                    "Trying to edit another seller profile here.",
                    "+201000000088"))).StatusCode);

        AuthenticationResponse sellerLogin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(applicant.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        Assert.Contains(RoleNames.Seller, sellerLogin.User.Roles);
    }

    [Fact]
    public async Task CustomerCannotAccessSellerProfileOrAdminSellerRoutes()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/seller/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/seller-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/sellers")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{Guid.CreateVersion7()}/suspend",
                new SuspendSellerRequest("Policy violation"))).StatusCode);
    }

    [Fact]
    public async Task Cancel_OwnPending_AllowsReplacement()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        HttpResponseMessage cancelled = await client.PostAsync(
            $"/api/v1/seller/applications/{pending.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        HttpResponseMessage replacement = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            ValidSubmit("Replacement Studio"));
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);

        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/seller/applications/{pending.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task SuspendAndReactivate_UseActivePolicy_AndKeepHistory()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        Authorize(client, adminToken);
        await client.PostAsync($"/api/v1/admin/seller-applications/{pending.Id}/approve", null);

        AuthenticationResponse sellerLogin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(applicant.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        Authorize(client, sellerLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/seller/access")).StatusCode);

        SellerProfileResponse profile = (await (await client.GetAsync("/api/v1/seller/profile")).Content
            .ReadFromJsonAsync<SellerProfileResponse>(JsonOptions))!;

        Authorize(client, sellerLogin.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{profile.Id}/suspend",
                new SuspendSellerRequest("Policy violation"))).StatusCode);

        Authorize(client, adminToken);
        HttpResponseMessage suspended = await client.PostAsJsonAsync(
            $"/api/v1/admin/sellers/{profile.Id}/suspend",
            new SuspendSellerRequest("Policy violation"));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        Authorize(client, sellerLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/seller/access")).StatusCode);

        HttpResponseMessage mine = await client.GetAsync("/api/v1/seller/applications/me");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        List<SellerApplicationResponse> history = (await mine.Content.ReadFromJsonAsync<List<SellerApplicationResponse>>(JsonOptions))!;
        Assert.Contains(history, a => a.Id == pending.Id && a.Status == nameof(SellerApplicationStatus.Approved));

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/sellers/{profile.Id}/reactivate", null)).StatusCode);

        Authorize(client, sellerLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/seller/access")).StatusCode);

        Assert.Contains(
            _factory.Emails.Sent,
            m => m.To == applicant.User.Email && m.Subject == "Your Seller Account Has Been Suspended");
        Assert.Contains(
            _factory.Emails.Sent,
            m => m.To == applicant.User.Email && m.Subject == "Your Seller Account Has Been Reactivated");
    }

    [Fact]
    public async Task EmailFailure_DoesNotRollBackApproval()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        _factory.Emails.ThrowOnSend = true;
        try
        {
            Authorize(client, adminToken);
            HttpResponseMessage approved = await client.PostAsync(
                $"/api/v1/admin/seller-applications/{pending.Id}/approve",
                null);
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        }
        finally
        {
            _factory.Emails.ThrowOnSend = false;
        }

        Authorize(client, applicant.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/seller/profile")).StatusCode);
    }

    [Fact]
    public async Task AdminList_SupportsStatusAndPagination()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        await SubmitAsync(client);

        Authorize(client, adminToken);
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/seller-applications?status=Pending&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<SellerApplicationResponse> page = (await response.Content
            .ReadFromJsonAsync<PagedResult<SellerApplicationResponse>>(JsonOptions))!;
        Assert.True(page.TotalCount >= 1);
        Assert.Equal(1, page.Page);
        Assert.All(page.Items, item => Assert.Equal(nameof(SellerApplicationStatus.Pending), item.Status));
    }

    [Fact]
    public async Task ConcurrentApproveAndReject_OneSucceeds()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        HttpClient approveClient = _factory.CreateMigratedClient();
        HttpClient rejectClient = _factory.CreateMigratedClient();
        Authorize(approveClient, adminToken);
        Authorize(rejectClient, adminToken);

        Task<HttpResponseMessage> approve = approveClient.PostAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/approve",
            null);
        Task<HttpResponseMessage> reject = rejectClient.PostAsJsonAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/reject",
            new RejectSellerApplicationRequest("Please provide a more detailed business description."));

        HttpResponseMessage[] results = await Task.WhenAll(approve, reject);
        int successes = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        int conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, successes);
        Assert.Equal(1, conflicts);

        Authorize(client, adminToken);
        SellerApplicationResponse latest = (await (await client.GetAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}")).Content
            .ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;

        if (latest.Status == nameof(SellerApplicationStatus.Approved))
        {
            Authorize(client, applicant.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/seller/profile")).StatusCode);
        }
        else
        {
            Assert.Equal(nameof(SellerApplicationStatus.Rejected), latest.Status);
            Authorize(client, applicant.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/seller/profile")).StatusCode);
        }
    }

    [Fact]
    public async Task Reject_RequiresReason()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        AuthenticationResponse applicant = await RegisterAsync(client);
        Authorize(client, applicant.AccessToken);
        SellerApplicationResponse pending = await SubmitAsync(client);

        Authorize(client, adminToken);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/seller-applications/{pending.Id}/reject",
            new RejectSellerApplicationRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static SubmitSellerApplicationRequest ValidSubmit(string? businessName = null)
    {
        return new SubmitSellerApplicationRequest(
            businessName ?? "Abdo Handmade",
            "Handmade accessories and crafts studio.",
            "+201000000001");
    }

    private static async Task<SellerApplicationResponse> SubmitAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/seller/applications", ValidSubmit());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        string email = $"seller_{Guid.NewGuid():N}@example.com";
        AuthenticationResponse registered = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        return registered;
    }

    private async Task<(AuthenticationResponse User, string AccessToken)> CreateAdminAsync(HttpClient client)
    {
        AuthenticationResponse registered = await RegisterAsync(client);
        await _factory.AssignRoleAsync(registered.User.Id, RoleNames.Admin);
        AuthenticationResponse admin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(registered.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        return (admin, admin.AccessToken);
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
