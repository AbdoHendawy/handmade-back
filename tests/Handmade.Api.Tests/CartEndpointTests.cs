using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class CartEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public CartEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_IsUnauthorized()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/cart")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(Guid.CreateVersion7(), null, 1)))
                .StatusCode);
    }

    [Fact]
    public async Task GetCart_WithoutItems_ReturnsEmptyCart()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse user = await RegisterAsync(client);
        Authorize(client, user.AccessToken);

        CartResponse cart = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Null(cart.Id);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.ItemCount);
        Assert.Equal(0m, cart.Total);
    }

    [Fact]
    public async Task AddSameProductTwice_IncrementsQuantity_AndLazyCreatesCart()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 100m);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);

        CartResponse first = await AddAsync(client, product.Id, 1);
        Assert.NotNull(first.Id);
        Assert.Single(first.Items);
        Assert.Equal(1, first.ItemCount);
        Assert.Equal(100m, first.Total);

        CartResponse second = await AddAsync(client, product.Id, 2);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(second.Items);
        Assert.Equal(3, second.Items[0].Quantity);
        Assert.Equal(3, second.ItemCount);
        Assert.Equal(300m, second.Total);
    }

    [Fact]
    public async Task UpdateRemoveAndClear_AffectOnlyOwnCart()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 40m);

        AuthenticationResponse owner = await RegisterAsync(client);
        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        await AddAsync(client, product.Id, 2);

        CartResponse updated = (await (await client.PutAsJsonAsync(
            $"/api/v1/cart/items/{product.Id}",
            new UpdateCartItemRequest(5))).Content.ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Equal(5, updated.Items[0].Quantity);
        Assert.Equal(200m, updated.Total);

        Authorize(client, other.AccessToken);
        CartResponse otherCart = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Empty(otherCart.Items);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/api/v1/cart/items/{product.Id}",
                new UpdateCartItemRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/cart/items/{product.Id}")).StatusCode);

        Authorize(client, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/cart/items/{product.Id}")).StatusCode);
        CartResponse afterRemove = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Empty(afterRemove.Items);

        await AddAsync(client, product.Id, 1);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/cart")).StatusCode);
        CartResponse cleared = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Empty(cleared.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/cart")).StatusCode);
    }

    [Fact]
    public async Task UnpublishedAndInvalidQuantity_AreRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        ProductResponse draft = await CreateDraftAsync(client, sellerToken, categoryId, 25m);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        HttpResponseMessage unpublished = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(draft.Id, null, 1));
        Assert.Equal(HttpStatusCode.BadRequest, unpublished.StatusCode);

        HttpResponseMessage missing = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(Guid.CreateVersion7(), null, 1));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        HttpResponseMessage invalid = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(draft.Id, null, 0));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task VariantIsRequired_WhenProductHasVariants()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 80m, withVariant: true);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(product.Id, null, 1)))
                .StatusCode);

        CartResponse cart = await AddAsync(client, product.Id, 1, product.VariantId);
        Assert.Equal(product.VariantId, cart.Items[0].VariantId);
        Assert.Equal(70m, cart.Items[0].UnitPrice);
    }

    [Fact]
    public async Task PriceChange_AndSuspendedSeller_AreVisibleOnGet()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 100m);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        CartResponse added = await AddAsync(client, product.Id, 1);
        Assert.False(added.Items[0].PriceChanged);

        Authorize(client, product.SellerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}",
                new UpdateProductRequest(
                    product.Name,
                    "Handmade leather bracelet with a brass clasp.",
                    product.CategoryId,
                    120m,
                    "EGP",
                    null))).StatusCode);

        Authorize(client, customer.AccessToken);
        CartResponse changed = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.True(changed.Items[0].PriceChanged);
        Assert.Equal(120m, changed.Items[0].UnitPrice);
        Assert.Equal(120m, changed.Total);

        Authorize(client, product.AdminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{product.SellerId}/suspend",
                new SuspendSellerRequest("Policy violation requires a temporary pause."))).StatusCode);

        Authorize(client, customer.AccessToken);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(product.Id, null, 1)))
                .StatusCode);
        CartResponse unavailable = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.False(unavailable.Items[0].IsAvailable);
        Assert.Equal("seller_not_active", unavailable.Items[0].UnavailabilityReason);
        Assert.Equal(0m, unavailable.Total);
        Assert.Equal(120m, unavailable.Subtotal);
    }

    [Fact]
    public async Task ConcurrentAdds_DoNotLoseQuantity()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 15m);
        AuthenticationResponse customer = await RegisterAsync(client);

        HttpClient first = _factory.CreateClient();
        HttpClient second = _factory.CreateClient();
        Authorize(first, customer.AccessToken);
        Authorize(second, customer.AccessToken);

        AddCartItemRequest request = new(product.Id, null, 1);
        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/cart/items", request),
            second.PostAsJsonAsync("/api/v1/cart/items", request));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        Authorize(client, customer.AccessToken);
        CartResponse cart = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Equal(2, cart.ItemCount);
        Assert.Single(cart.Items);
        Assert.Equal(2, cart.Items[0].Quantity);
    }

    private async Task<PublishedProduct> PublishProductAsync(
        HttpClient client,
        decimal price,
        bool withVariant = false)
    {
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        ProductResponse product = await CreateDraftAsync(client, sellerToken, categoryId, price);
        Authorize(client, sellerToken);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("products/cart.jpg", "https://cdn.local/cart.jpg", 1, true))).StatusCode);

        Guid? variantId = null;
        if (withVariant)
        {
            ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest("Small", "CRT-" + Guid.NewGuid().ToString("N")[..8], 70m, "EGP")))
                .Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
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

    private static async Task<ProductResponse> CreateDraftAsync(
        HttpClient client,
        string sellerToken,
        Guid categoryId,
        decimal price)
    {
        Authorize(client, sellerToken);
        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Cart Test Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                price,
                "EGP",
                null))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        return product;
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
        string email = $"cart_{Guid.NewGuid():N}@example.com";
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
