using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class OrderEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly CheckoutRequest Delivery = new(
        "Nour Hassan",
        "+201001234567",
        "12 Nile Street",
        "Apt 4",
        "Cairo",
        "Cairo",
        "11511",
        "Leave at the door");

    private readonly HandmadeApiFactory _factory;

    public OrderEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_RequiresAuthentication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/checkout", Delivery)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/orders")).StatusCode);
    }

    [Fact]
    public async Task EmptyCart_ReturnsCartEmpty_AndPersistsNothing()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderErrorCodes.CartEmpty, await ReadCodeAsync(response));

        PagedResult<OrderGroupListItemResponse> page = (await (await client.GetAsync("/api/v1/orders")).Content
            .ReadFromJsonAsync<PagedResult<OrderGroupListItemResponse>>(JsonOptions))!;
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Checkout_SplitsSellers_UsesLivePrice_AndKeepsCartRow()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct first = await PublishProductAsync(client, 100m, stock: 5);
        PublishedProduct second = await PublishProductAsync(client, 70m, stock: 5);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, first.Id, 2);
        await AddAsync(client, second.Id, 1);

        Authorize(client, first.SellerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{first.Id}",
                new UpdateProductRequest(
                    first.Name,
                    "Handmade leather bracelet with a brass clasp.",
                    first.CategoryId,
                    120m,
                    "EGP",
                    null,
                    5))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage placed = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.NotNull(placed.Headers.Location);
        OrderGroupResponse group = (await placed.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(2, group.Orders.Count);
        Assert.Equal(310m, group.Subtotal);
        Assert.Equal(group.Subtotal, group.Total);
        Assert.Equal("Placed", group.Status);
        Assert.True(group.Number > 0);
        Assert.Contains(group.Orders, o => o.SellerId == first.SellerId && o.Items[0].UnitPrice == 120m);
        Assert.Contains(group.Orders, o => o.SellerId == second.SellerId);
        Assert.All(group.Orders, o => Assert.Equal("Placed", o.Status));
        Assert.DoesNotContain(group.Orders, o => o.Items.Select(i => i.SellerId).Distinct().Count() > 1);
        Assert.Contains(group.Id.ToString(), placed.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        CartResponse cart = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Empty(cart.Items);
        Assert.NotNull(cart.Id);

        OrderGroupResponse loaded = (await (await client.GetAsync($"/api/v1/orders/{group.Id}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(group.Id, loaded.Id);
        Assert.Equal(120m, loaded.Orders.Single(o => o.SellerId == first.SellerId).Items[0].UnitPrice);

        Authorize(client, first.SellerToken);
        PagedResult<OrderResponse> sellerPage = (await (await client.GetAsync("/api/v1/seller/orders")).Content
            .ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions))!;
        Assert.Contains(sellerPage.Items, o => o.SellerId == first.SellerId);
        Assert.DoesNotContain(sellerPage.Items, o => o.SellerId == second.SellerId);

        Authorize(client, customer.AccessToken);
        PagedResult<NotificationResponse> inbox = (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Assert.Contains(inbox.Items, n => n.Type == NotificationTypes.OrderPlaced);
    }

    [Fact]
    public async Task Snapshots_SurviveCatalogEdits()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 40m, stock: 3, withVariant: true);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 1, product.VariantId);

        OrderGroupResponse group = await CheckoutAsync(client);
        OrderItemResponse item = Assert.Single(group.Orders.SelectMany(o => o.Items));
        Assert.Equal(product.VariantId, item.VariantId);
        Assert.False(string.IsNullOrWhiteSpace(item.Sku));
        string originalName = item.ProductName;

        Authorize(client, product.SellerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}",
                new UpdateProductRequest(
                    "Renamed After Order",
                    "Handmade leather bracelet with a brass clasp.",
                    product.CategoryId,
                    99m,
                    "EGP",
                    null,
                    3))).StatusCode);

        Authorize(client, customer.AccessToken);
        OrderGroupResponse loaded = (await (await client.GetAsync($"/api/v1/orders/{group.Id}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(originalName, loaded.Orders[0].Items[0].ProductName);
        Assert.Equal(40m, loaded.Orders[0].Items[0].UnitPrice);
    }

    [Fact]
    public async Task FailedCheckout_LeavesCartUnchanged()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 25m, stock: 1);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 2);

        HttpResponseMessage stock = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, stock.StatusCode);
        Assert.Equal("insufficient_stock", await ReadCodeAsync(stock));
        Assert.Single((await GetCartAsync(client)).Items);

        Authorize(client, product.AdminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{product.SellerId}/suspend",
                new SuspendSellerRequest("Policy violation requires a temporary pause."))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage inactive = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(inactive));
        Assert.Single((await GetCartAsync(client)).Items);
    }

    [Fact]
    public async Task UnpublishedAndMissingVariant_AreRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct unpublished = await PublishProductAsync(client, 30m, stock: 4);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, unpublished.Id, 1);
        HttpResponseMessage archive = await ArchiveAsync(client, unpublished);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage unpublishedResponse = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, unpublishedResponse.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(unpublishedResponse));

        await client.DeleteAsync("/api/v1/cart");
        PublishedProduct needsVariant = await PublishProductAsync(client, 55m, stock: 4);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, needsVariant.Id, 1);
        Authorize(client, needsVariant.SellerToken);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{needsVariant.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "ORD-" + Guid.NewGuid().ToString("N")[..8],
                    55m,
                    "EGP",
                    4))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage missingVariant = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, missingVariant.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(missingVariant));
        Assert.Single((await GetCartAsync(client)).Items);
    }

    [Fact]
    public async Task CrossAccess_IsNotFound()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 15m, stock: 2);
        AuthenticationResponse owner = await RegisterAsync(client);
        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        await AddAsync(client, product.Id, 1);
        OrderGroupResponse group = await CheckoutAsync(client);

        Authorize(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/orders/{group.Id}")).StatusCode);

        Guid orderId = group.Orders[0].Id;
        PublishedProduct otherSellerProduct = await PublishProductAsync(client, 18m, stock: 2);
        Authorize(client, otherSellerProduct.SellerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/seller/orders/{orderId}")).StatusCode);

        Authorize(client, product.SellerToken);
        OrderResponse sellerOrder = (await (await client.GetAsync($"/api/v1/seller/orders/{orderId}")).Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Equal(orderId, sellerOrder.Id);
    }

    [Fact]
    public async Task ConcurrentCheckouts_DoNotOversell()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 10m, stock: 1);
        AuthenticationResponse firstUser = await RegisterAsync(client);
        AuthenticationResponse secondUser = await RegisterAsync(client);

        HttpClient first = _factory.CreateClient();
        HttpClient second = _factory.CreateClient();
        Authorize(first, firstUser.AccessToken);
        Authorize(second, secondUser.AccessToken);
        await AddAsync(first, product.Id, 1);
        await AddAsync(second, product.Id, 1);

        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/checkout", Delivery),
            second.PostAsJsonAsync("/api/v1/checkout", Delivery));

        int created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        int rejected = responses.Count(r => r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        Assert.Equal(1, created);
        Assert.Equal(1, rejected);
        HttpResponseMessage failed = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        string? code = await ReadCodeAsync(failed);
        Assert.True(
            code is "insufficient_stock" or OrderErrorCodes.ConcurrencyConflict,
            code);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(HttpClient client, PublishedProduct product)
    {
        Authorize(client, product.AdminToken);
        return await client.PostAsync($"/api/v1/admin/products/{product.Id}/archive", null);
    }

    private static async Task<OrderGroupResponse> CheckoutAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
    }

    private static async Task<CartResponse> GetCartAsync(HttpClient client)
    {
        return (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private async Task<PublishedProduct> PublishProductAsync(
        HttpClient client,
        decimal price,
        int stock,
        bool withVariant = false)
    {
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);
        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Order Test Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                price,
                "EGP",
                null,
                withVariant ? 0 : stock))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("products/order.jpg", "https://cdn.local/order.jpg", 1, true))).StatusCode);

        Guid? variantId = null;
        if (withVariant)
        {
            ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "ORD-" + Guid.NewGuid().ToString("N")[..8],
                    price,
                    "EGP",
                    stock))).Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
            variantId = variant.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        return new PublishedProduct(
            product.Id,
            product.Name,
            product.SellerId,
            categoryId,
            variantId,
            sellerToken,
            adminToken);
    }

    private static async Task<CartResponse> AddAsync(
        HttpClient client,
        Guid productId,
        int quantity,
        Guid? variantId = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(productId, variantId, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>(JsonOptions))!;
    }

    private static async Task<Guid> FirstCategoryIdAsync(HttpClient client)
    {
        List<CategoryTreeResponse> tree = (await (await client.GetAsync("/api/v1/catalog/categories")).Content
            .ReadFromJsonAsync<List<CategoryTreeResponse>>(JsonOptions))!;
        Assert.NotEmpty(tree);
        return tree[0].Id;
    }

    private async Task<string> ApproveSellerAsync(
        HttpClient client,
        AuthenticationResponse applicant,
        string adminToken)
    {
        Authorize(client, applicant.AccessToken);
        HttpResponseMessage submit = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Studio " + Guid.NewGuid().ToString("N")[..8],
                "Handmade accessories and crafts studio.",
                "+201000000001"));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        SellerApplicationResponse application =
            (await submit.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", null)).StatusCode);
        return applicant.AccessToken;
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

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        string email = $"order_{Guid.NewGuid():N}@example.com";
        return (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private sealed record PublishedProduct(
        Guid Id,
        string Name,
        Guid SellerId,
        Guid CategoryId,
        Guid? VariantId,
        string SellerToken,
        string AdminToken);
}
