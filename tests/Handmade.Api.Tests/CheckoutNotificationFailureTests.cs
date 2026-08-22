using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Handmade.Api.Tests;

[Collection(nameof(NotificationFailureApiCollection))]
public sealed class CheckoutNotificationFailureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ThrowingNotificationApiFactory _factory;

    public CheckoutNotificationFailureTests(ThrowingNotificationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NotifyThrow_StillReturns201()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/v1/cart/items",
                new AddCartItemRequest(product.Id, null, 1))).StatusCode);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/checkout",
            new CheckoutRequest(
                "Nour Hassan",
                "+201001234567",
                "12 Nile Street",
                null,
                "Cairo",
                "Cairo",
                null,
                null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderGroupResponse group = (await response.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("Placed", group.Status);
        Assert.Single(group.Orders);
    }

    private async Task<PublishedProduct> PublishProductAsync(HttpClient client)
    {
        AuthenticationResponse adminUser = await RegisterAsync(client);
        await _factory.AssignRoleAsync(adminUser.User.Id, RoleNames.Admin);
        AuthenticationResponse admin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(adminUser.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        List<CategoryTreeResponse> tree = (await (await client.GetAsync("/api/v1/catalog/categories")).Content
            .ReadFromJsonAsync<List<CategoryTreeResponse>>(JsonOptions))!;
        Guid categoryId = tree[0].Id;

        AuthenticationResponse sellerUser = await RegisterAsync(client);
        Authorize(client, sellerUser.AccessToken);
        HttpResponseMessage submit = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Studio " + Guid.NewGuid().ToString("N")[..8],
                "Handmade accessories and crafts studio.",
                "+201000000001"));
        SellerApplicationResponse application =
            (await submit.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;
        Authorize(client, admin.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", null)).StatusCode);

        Authorize(client, sellerUser.AccessToken);
        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Notify Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                20m,
                "EGP",
                null,
                3))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("products/notify.jpg", "https://cdn.local/notify.jpg", 1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        return new PublishedProduct(product.Id);
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

    private sealed record PublishedProduct(Guid Id);
}

public sealed class ThrowingNotificationApiFactory : HandmadeApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<INotificationPublisher, ThrowingNotificationPublisher>();
        });
    }
}

public sealed class ThrowingNotificationPublisher : INotificationPublisher
{
    public Task<NotificationResponse> PublishToUserAsync(
        CreateUserNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Type.StartsWith("order.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Notification store failed.");
        }

        return Task.FromResult(new NotificationResponse(
            Guid.CreateVersion7(),
            request.UserId,
            request.Type,
            request.Title,
            request.Body,
            request.DataJson,
            false,
            null,
            "Pending",
            DateTimeOffset.UtcNow));
    }

    public Task PublishToRoleAsync(
        string roleName,
        string type,
        string title,
        string body,
        string idempotencyPrefix,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (type.StartsWith("order.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Notification store failed.");
        }

        return Task.CompletedTask;
    }
}

[CollectionDefinition(nameof(NotificationFailureApiCollection))]
public sealed class NotificationFailureApiCollection : ICollectionFixture<ThrowingNotificationApiFactory>;
