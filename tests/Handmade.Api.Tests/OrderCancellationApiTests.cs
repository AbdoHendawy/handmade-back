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
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class OrderCancellationApiTests
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

    private static readonly string[] ForbiddenContractNames =
    [
        "stock",
        "restoredstock",
        "stockrestored",
        "inventory",
        "inventorydelta"
    ];

    private readonly HandmadeApiFactory _factory;

    public OrderCancellationApiTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancelAsync_CustomerCancel_RestoresProductStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedState before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Placed, before.Status);
        Assert.Equal(8, before.ProductStock);

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        OrderResponse body = (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Equal("Cancelled", body.Status);
        Assert.Equal(placed.OrderId, body.Id);
        Assert.NotEmpty(body.Items);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.ProductStock);
        Assert.True(after.ProductStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_SellerCancel_RestoresProductStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedState before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(8, before.ProductStock);

        Authorize(client, placed.Product.SellerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        OrderResponse body = (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Equal("Cancelled", body.Status);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.ProductStock);
        Assert.True(after.ProductStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_CustomerCancel_RestoresVariantStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2, withVariant: true);
        Guid variantId = placed.Product.VariantId!.Value;
        PersistedState before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(OrderStatus.Placed, before.Status);
        Assert.Equal(0, before.ProductStock);
        Assert.Equal(8, before.VariantStock);

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.VariantStock);
        Assert.Equal(0, after.ProductStock);
        Assert.True(after.VariantStock >= 0);
        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Guid? lineVariantId = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == placed.OrderId)
            .Select(i => i.VariantId)
            .SingleAsync();
        Assert.Equal(variantId, lineVariantId);
    }

    [Fact]
    public async Task CancelAsync_SellerCancel_RestoresVariantStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2, withVariant: true);
        Guid variantId = placed.Product.VariantId!.Value;
        PersistedState before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(8, before.VariantStock);
        Assert.Equal(0, before.ProductStock);

        Authorize(client, placed.Product.SellerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.VariantStock);
        Assert.Equal(0, after.ProductStock);
        Assert.True(after.VariantStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_MultiSellerOrder_RestoresOnlyCancelledSellerStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct productA = await PublishProductAsync(client, 15m, stock: 10);
        PublishedProduct productB = await PublishProductAsync(client, 20m, stock: 10);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, productA.Id, 2);
        await AddAsync(client, productB.Id, 3);
        OrderGroupResponse group = await CheckoutAsync(client);
        OrderResponse orderA = group.Orders.Single(o => o.SellerId == productA.SellerId);
        OrderResponse orderB = group.Orders.Single(o => o.SellerId == productB.SellerId);

        PersistedState beforeA = await ReadPersistedAsync(orderA.Id, productA.Id, null);
        PersistedState beforeB = await ReadPersistedAsync(orderB.Id, productB.Id, null);
        Assert.Equal(8, beforeA.ProductStock);
        Assert.Equal(7, beforeB.ProductStock);
        Assert.Equal(OrderStatus.Placed, beforeA.Status);
        Assert.Equal(OrderStatus.Placed, beforeB.Status);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage response = await client.PostAsync($"/api/v1/orders/{orderA.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PersistedState afterA = await ReadPersistedAsync(orderA.Id, productA.Id, null);
        PersistedState afterB = await ReadPersistedAsync(orderB.Id, productB.Id, null);
        Assert.Equal(OrderStatus.Cancelled, afterA.Status);
        Assert.Equal(10, afterA.ProductStock);
        Assert.Equal(OrderStatus.Placed, afterB.Status);
        Assert.Equal(7, afterB.ProductStock);
        Assert.True(afterA.ProductStock >= 0);
        Assert.True(afterB.ProductStock >= 0);

        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        OrderGroup persistedGroup = await db.OrderGroups.AsNoTracking().SingleAsync(g => g.Id == group.Id);
        Assert.Equal(OrderGroupStatus.Placed, persistedGroup.Status);
    }

    [Fact]
    public async Task CancelAsync_CrossCustomerOrder_Returns404_AndDoesNotRestoreStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, other.AccessToken);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrderErrorCodes.OrderNotFound, await ReadCodeAsync(response));

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Placed, after.Status);
        Assert.Equal(8, after.ProductStock);
    }

    [Fact]
    public async Task CancelAsync_CrossSellerOrder_Returns404_AndDoesNotRestoreStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        AuthenticationResponse otherSeller = await RegisterAsync(client);
        await ApproveSellerAsync(client, otherSeller, placed.Product.AdminToken);
        Authorize(client, otherSeller.AccessToken);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Placed, after.Status);
        Assert.Equal(8, after.ProductStock);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_Returns409_AndDoesNotRestoreAgain()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.CustomerToken);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        PersistedState afterFirst = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, afterFirst.Status);
        Assert.Equal(10, afterFirst.ProductStock);

        HttpResponseMessage second = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(second));

        PersistedState afterSecond = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, afterSecond.Status);
        Assert.Equal(10, afterSecond.ProductStock);
    }

    [Fact]
    public async Task CancelAsync_AfterConfirmed_Returns409_AndDoesNotRestoreStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.Product.SellerToken);
        await TransitionAsync(client, placed.OrderId, "confirm");

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(response));

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Confirmed, after.Status);
        Assert.Equal(8, after.ProductStock);
    }

    [Fact]
    public async Task SellerCancel_AfterPreparing_Returns409_AndDoesNotRestoreStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.Product.SellerToken);
        await TransitionAsync(client, placed.OrderId, "confirm");
        await TransitionAsync(client, placed.OrderId, "prepare");

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(response));

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Preparing, after.Status);
        Assert.Equal(8, after.ProductStock);
    }

    [Fact]
    public async Task SellerCancel_AfterShipped_Returns409_AndDoesNotRestoreStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.Product.SellerToken);
        await TransitionAsync(client, placed.OrderId, "confirm");
        await TransitionAsync(client, placed.OrderId, "prepare");
        await TransitionAsync(client, placed.OrderId, "ship");

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(response));

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Shipped, after.Status);
        Assert.Equal(8, after.ProductStock);
    }

    [Fact]
    public async Task CustomerCancel_DoesNotChangeOrderResponseContract()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        OrderResponse body = JsonSerializer.Deserialize<OrderResponse>(json, JsonOptions)!;
        Assert.Equal(placed.OrderId, body.Id);
        Assert.Equal(placed.GroupId, body.OrderGroupId);
        Assert.True(body.Number > 0);
        Assert.Equal("Cancelled", body.Status);
        Assert.Equal(placed.Product.SellerId, body.SellerId);
        Assert.False(string.IsNullOrWhiteSpace(body.SellerName));
        Assert.Equal("EGP", body.Currency);
        Assert.True(body.Subtotal > 0);
        Assert.True(body.Total > 0);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEmpty(body.Items);

        using JsonDocument document = JsonDocument.Parse(json);
        AssertNoForbiddenContractNames(document.RootElement);
    }

    [Fact]
    public async Task CustomerCancel_PersistsCancelledOrderAndRestoredStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedState before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(8, before.ProductStock);

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.Dispose();

        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Order order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == placed.OrderId);
        List<OrderItem> items = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == placed.OrderId)
            .ToListAsync();
        Product product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == placed.Product.Id);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotEmpty(items);
        int restored = items.Sum(i => i.Quantity);
        Assert.Equal(before.ProductStock + restored, product.StockQuantity);
        Assert.Equal(10, product.StockQuantity);
        Assert.True(product.StockQuantity >= 0);
    }

    [Fact]
    public async Task CustomerCancel_NotificationStillPublishedAfterSuccessfulSave()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PersistedState after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.ProductStock);

        Authorize(client, placed.Product.SellerToken);
        PagedResult<NotificationResponse> sellerInbox = (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        NotificationResponse cancelled = Assert.Single(
            sellerInbox.Items,
            n => n.Type == NotificationTypes.OrderCancelled);
        Assert.Equal("order.cancelled", cancelled.Type);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == "stock.restored");
        Assert.Contains(placed.OrderId.ToString("D"), cancelled.DataJson, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PersistedState> ReadPersistedAsync(Guid orderId, Guid productId, Guid? variantId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Order order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Product product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        int? variantStock = null;
        if (variantId is Guid id)
        {
            variantStock = await db.ProductVariants
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => v.StockQuantity)
                .SingleAsync();
        }

        return new PersistedState(order.Status, product.StockQuantity, variantStock);
    }

    private async Task<PlacedOrder> PlaceOrderAsync(
        HttpClient client,
        int stock,
        int quantity,
        bool withVariant = false)
    {
        PublishedProduct product = await PublishProductAsync(client, 15m, stock, withVariant);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, quantity, product.VariantId);
        OrderGroupResponse group = await CheckoutAsync(client);
        return new PlacedOrder(product, customer.AccessToken, group.Orders[0].Id, group.Id);
    }

    private static async Task<OrderGroupResponse> CheckoutAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
    }

    private static async Task<OrderResponse> TransitionAsync(HttpClient client, Guid orderId, string action)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/v1/seller/orders/{orderId}/{action}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
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
                "Cancel Api Bracelet",
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
                new AddProductImageRequest("products/cancel-api.jpg", "https://cdn.local/cancel-api.jpg", 1, true)))
            .StatusCode);

        Guid? variantId = null;
        if (withVariant)
        {
            ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "API-" + Guid.NewGuid().ToString("N")[..8],
                    price,
                    "EGP",
                    stock))).Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
            variantId = variant.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        return new PublishedProduct(product.Id, product.SellerId, variantId, sellerToken, adminToken);
    }

    private static async Task AddAsync(
        HttpClient client,
        Guid productId,
        int quantity,
        Guid? variantId = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(productId, variantId, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        string email = $"cancel_api_{Guid.NewGuid():N}@example.com";
        return (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private static void AssertNoForbiddenContractNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();
                Assert.DoesNotContain(normalized, ForbiddenContractNames);
                AssertNoForbiddenContractNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                AssertNoForbiddenContractNames(child);
            }
        }
    }

    private sealed record PlacedOrder(PublishedProduct Product, string CustomerToken, Guid OrderId, Guid GroupId);

    private sealed record PublishedProduct(
        Guid Id,
        Guid SellerId,
        Guid? VariantId,
        string SellerToken,
        string AdminToken);

    private sealed record PersistedState(OrderStatus Status, int ProductStock, int? VariantStock);
}
