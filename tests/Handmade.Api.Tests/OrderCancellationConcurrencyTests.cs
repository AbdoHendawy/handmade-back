using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Orders;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class OrderCancellationConcurrencyTests
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

    public OrderCancellationConcurrencyTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancelAsync_RestoresStock_WhenProductXminConflicts()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedInventory before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Placed, before.Status);
        Assert.Equal(8, before.ProductStock);

        using (_factory.CancellationXminConflicts.ArmProduct(placed.Product.Id, 10))
        {
            Authorize(client, placed.CustomerToken);
            HttpResponseMessage response = await client.PostAsync(
                $"/api/v1/orders/{placed.OrderId}/cancel",
                null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        PersistedInventory after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(12, after.ProductStock);
        Assert.True(after.ProductStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_RestoresVariantStock_WhenVariantXminConflicts()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2, withVariant: true);
        Guid variantId = placed.Product.VariantId!.Value;
        PersistedInventory before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(OrderStatus.Placed, before.Status);
        Assert.Equal(0, before.ProductStock);
        Assert.Equal(8, before.VariantStock);

        using (_factory.CancellationXminConflicts.ArmVariant(variantId, 10))
        {
            Authorize(client, placed.CustomerToken);
            HttpResponseMessage response = await client.PostAsync(
                $"/api/v1/orders/{placed.OrderId}/cancel",
                null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        PersistedInventory after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, variantId);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(0, after.ProductStock);
        Assert.Equal(12, after.VariantStock);
        Assert.True(after.VariantStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_AndCheckout_ConcurrentSameSku_NoLostUpdate()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedInventory before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(8, before.ProductStock);

        AuthenticationResponse buyer = await RegisterAsync(client);
        HttpClient cancelClient = _factory.CreateClient();
        HttpClient checkoutClient = _factory.CreateClient();
        Authorize(cancelClient, placed.CustomerToken);
        Authorize(checkoutClient, buyer.AccessToken);
        await AddAsync(checkoutClient, placed.Product.Id, 3);

        HttpResponseMessage[] responses = await Task.WhenAll(
            cancelClient.PostAsync($"/api/v1/orders/{placed.OrderId}/cancel", null),
            checkoutClient.PostAsJsonAsync("/api/v1/checkout", Delivery));
        HttpResponseMessage cancelResponse = responses[0];
        HttpResponseMessage checkoutResponse = responses[1];

        bool cancelSucceeded = cancelResponse.StatusCode == HttpStatusCode.OK;
        bool checkoutSucceeded = checkoutResponse.StatusCode == HttpStatusCode.Created;
        if (!cancelSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
            string? cancelCode = await ReadCodeAsync(cancelResponse);
            Assert.True(
                cancelCode is OrderErrorCodes.ConcurrencyConflict or OrderErrorCodes.InvalidStatusTransition,
                cancelCode);
        }

        if (!checkoutSucceeded)
        {
            Assert.True(
                checkoutResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
                checkoutResponse.StatusCode.ToString());
            string? checkoutCode = await ReadCodeAsync(checkoutResponse);
            Assert.True(
                checkoutCode is "insufficient_stock" or OrderErrorCodes.ConcurrencyConflict,
                checkoutCode);
        }

        PersistedInventory after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        int expected = 8 + (cancelSucceeded ? 2 : 0) - (checkoutSucceeded ? 3 : 0);
        Assert.Equal(expected, after.ProductStock);
        Assert.True(after.ProductStock >= 0);
        Assert.Equal(cancelSucceeded ? OrderStatus.Cancelled : OrderStatus.Placed, after.Status);
        if (checkoutSucceeded)
        {
            Assert.Equal("Placed", (await checkoutResponse.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!
                .Orders[0].Status);
        }
    }

    [Fact]
    public async Task CancelAsync_AndSetStock_Concurrent_NoLostUpdate()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        const int setStockTo = 20;
        const int postCheckout = 8;
        const int quantity = 2;

        HttpClient cancelClient = _factory.CreateClient();
        HttpClient stockClient = _factory.CreateClient();
        Authorize(cancelClient, placed.CustomerToken);
        Authorize(stockClient, placed.Product.SellerToken);

        HttpResponseMessage[] responses = await Task.WhenAll(
            cancelClient.PostAsync($"/api/v1/orders/{placed.OrderId}/cancel", null),
            stockClient.PutAsJsonAsync(
                $"/api/v1/seller/products/{placed.Product.Id}/stock",
                new SetStockRequest(setStockTo)));
        HttpResponseMessage cancelResponse = responses[0];
        HttpResponseMessage stockResponse = responses[1];

        bool cancelSucceeded = cancelResponse.StatusCode == HttpStatusCode.OK;
        bool setStockSucceeded = stockResponse.StatusCode == HttpStatusCode.OK;
        if (!cancelSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
            string? cancelCode = await ReadCodeAsync(cancelResponse);
            Assert.True(
                cancelCode is OrderErrorCodes.ConcurrencyConflict or OrderErrorCodes.InvalidStatusTransition,
                cancelCode);
        }

        if (!setStockSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, stockResponse.StatusCode);
            Assert.Equal(CatalogErrorCodes.ConcurrencyConflict, await ReadCodeAsync(stockResponse));
        }

        PersistedInventory after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.True(after.ProductStock >= 0);
        if (cancelSucceeded)
        {
            Assert.Equal(OrderStatus.Cancelled, after.Status);
            Assert.True(
                after.ProductStock is postCheckout + quantity or setStockTo or setStockTo + quantity,
                after.ProductStock.ToString());
            Assert.NotEqual(postCheckout, after.ProductStock);
        }
        else
        {
            Assert.Equal(OrderStatus.Placed, after.Status);
            Assert.True(
                after.ProductStock is postCheckout or setStockTo,
                after.ProductStock.ToString());
            Assert.NotEqual(postCheckout + quantity, after.ProductStock);
            Assert.NotEqual(setStockTo + quantity, after.ProductStock);
        }
    }

    [Fact]
    public async Task ConcurrentCancel_SameOrder_RestoresStockExactlyOnce()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        PersistedInventory before = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(8, before.ProductStock);

        HttpClient first = _factory.CreateClient();
        HttpClient second = _factory.CreateClient();
        Authorize(first, placed.CustomerToken);
        Authorize(second, placed.CustomerToken);

        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsync($"/api/v1/orders/{placed.OrderId}/cancel", null),
            second.PostAsync($"/api/v1/orders/{placed.OrderId}/cancel", null));

        int succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        int rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, succeeded);
        Assert.Equal(1, rejected);
        HttpResponseMessage failed = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        string? code = await ReadCodeAsync(failed);
        Assert.True(
            code is OrderErrorCodes.InvalidStatusTransition or OrderErrorCodes.ConcurrencyConflict,
            code);

        PersistedInventory after = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, after.Status);
        Assert.Equal(10, after.ProductStock);
        Assert.True(after.ProductStock >= 0);
    }

    [Fact]
    public async Task CancelAsync_AfterAlreadyCancelled_DoesNotRestoreAgain()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedOrder placed = await PlaceOrderAsync(client, stock: 10, quantity: 2);
        Authorize(client, placed.CustomerToken);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        PersistedInventory afterFirst = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, afterFirst.Status);
        Assert.Equal(10, afterFirst.ProductStock);

        HttpResponseMessage second = await client.PostAsync(
            $"/api/v1/orders/{placed.OrderId}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(second));

        PersistedInventory afterSecond = await ReadPersistedAsync(placed.OrderId, placed.Product.Id, null);
        Assert.Equal(OrderStatus.Cancelled, afterSecond.Status);
        Assert.Equal(10, afterSecond.ProductStock);
    }

    [Fact]
    public async Task ConcurrentCancel_DifferentOrdersSameSku_RestoresEachQuantity()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 15m, stock: 8);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 2);
        Guid orderAId = (await CheckoutAsync(client)).Orders[0].Id;
        await AddAsync(client, product.Id, 1);
        Guid orderBId = (await CheckoutAsync(client)).Orders[0].Id;

        PersistedInventory beforeA = await ReadPersistedAsync(orderAId, product.Id, null);
        Assert.Equal(5, beforeA.ProductStock);
        Assert.Equal(OrderStatus.Placed, beforeA.Status);
        Assert.Equal(OrderStatus.Placed, (await ReadPersistedAsync(orderBId, product.Id, null)).Status);

        HttpClient first = _factory.CreateClient();
        HttpClient second = _factory.CreateClient();
        Authorize(first, customer.AccessToken);
        Authorize(second, customer.AccessToken);
        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsync($"/api/v1/orders/{orderAId}/cancel", null),
            second.PostAsync($"/api/v1/orders/{orderBId}/cancel", null));

        bool aSucceeded = responses[0].StatusCode == HttpStatusCode.OK;
        bool bSucceeded = responses[1].StatusCode == HttpStatusCode.OK;
        if (!aSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, responses[0].StatusCode);
            string? code = await ReadCodeAsync(responses[0]);
            Assert.True(
                code is OrderErrorCodes.ConcurrencyConflict or OrderErrorCodes.InvalidStatusTransition,
                code);
        }

        if (!bSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, responses[1].StatusCode);
            string? code = await ReadCodeAsync(responses[1]);
            Assert.True(
                code is OrderErrorCodes.ConcurrencyConflict or OrderErrorCodes.InvalidStatusTransition,
                code);
        }

        PersistedInventory afterA = await ReadPersistedAsync(orderAId, product.Id, null);
        PersistedInventory afterB = await ReadPersistedAsync(orderBId, product.Id, null);
        int expected = 5 + (aSucceeded ? 2 : 0) + (bSucceeded ? 1 : 0);
        Assert.Equal(expected, afterA.ProductStock);
        Assert.True(afterA.ProductStock >= 0);
        Assert.Equal(aSucceeded ? OrderStatus.Cancelled : OrderStatus.Placed, afterA.Status);
        Assert.Equal(bSucceeded ? OrderStatus.Cancelled : OrderStatus.Placed, afterB.Status);
        if (aSucceeded && bSucceeded)
        {
            Assert.Equal(8, afterA.ProductStock);
        }
    }

    private async Task<PersistedInventory> ReadPersistedAsync(Guid orderId, Guid productId, Guid? variantId)
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

        return new PersistedInventory(order.Status, product.StockQuantity, variantStock);
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
        return new PlacedOrder(product, customer.AccessToken, group.Orders[0].Id);
    }

    private static async Task<OrderGroupResponse> CheckoutAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
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
                "Cancel Concurrency Bracelet",
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
                new AddProductImageRequest("products/cancel.jpg", "https://cdn.local/cancel.jpg", 1, true))).StatusCode);

        Guid? variantId = null;
        if (withVariant)
        {
            ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "CXL-" + Guid.NewGuid().ToString("N")[..8],
                    price,
                    "EGP",
                    stock))).Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
            variantId = variant.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        return new PublishedProduct(product.Id, product.SellerId, variantId, sellerToken);
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
        string email = $"cancel_{Guid.NewGuid():N}@example.com";
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

    private sealed record PlacedOrder(PublishedProduct Product, string CustomerToken, Guid OrderId);

    private sealed record PublishedProduct(Guid Id, Guid SellerId, Guid? VariantId, string SellerToken);

    private sealed record PersistedInventory(OrderStatus Status, int ProductStock, int? VariantStock);
}
