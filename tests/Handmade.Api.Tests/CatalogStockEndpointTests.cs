using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class CatalogStockEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public CatalogStockEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAndUpdate_AcceptStock_AndDefaultToZero()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (Guid categoryId, string sellerToken, _) = await SellerContextAsync(client);
        Authorize(client, sellerToken);

        ProductResponse created = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Stock Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                100m,
                "EGP",
                null))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(0, created.StockQuantity);

        ProductResponse withStock = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Stocked Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                100m,
                "EGP",
                null,
                9))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(9, withStock.StockQuantity);

        ProductResponse updated = (await (await client.PutAsJsonAsync(
            $"/api/v1/seller/products/{withStock.Id}",
            new UpdateProductRequest(
                withStock.Name,
                withStock.Description,
                categoryId,
                withStock.Price,
                withStock.Currency,
                withStock.Slug,
                4))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(4, updated.StockQuantity);
    }

    [Fact]
    public async Task Variants_DefaultAndAcceptStock()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (Guid categoryId, string sellerToken, _) = await SellerContextAsync(client);
        Authorize(client, sellerToken);
        ProductResponse product = await CreateDraftAsync(client, categoryId);

        ProductVariantResponse created = (await (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants",
            new CreateProductVariantRequest("Small", "STK-" + Guid.NewGuid().ToString("N")[..8], 80m, "EGP")))
            .Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
        Assert.Equal(0, created.StockQuantity);

        ProductVariantResponse withStock = (await (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants",
            new CreateProductVariantRequest("Medium", "STK-" + Guid.NewGuid().ToString("N")[..8], 90m, "EGP", 6)))
            .Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
        Assert.Equal(6, withStock.StockQuantity);

        ProductVariantResponse updated = (await (await client.PutAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants/{withStock.Id}",
            new UpdateProductVariantRequest(withStock.Name, withStock.Sku, withStock.Price, withStock.Currency, 3)))
            .Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
        Assert.Equal(3, updated.StockQuantity);
    }

    [Fact]
    public async Task NegativeStock_IsRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (Guid categoryId, string sellerToken, _) = await SellerContextAsync(client);
        Authorize(client, sellerToken);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Bad Stock",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                100m,
                "EGP",
                null,
                -1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SellerCanUpdateOwnStock_OtherSellerGetsNotFound()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (Guid categoryId, string sellerToken, string adminToken) = await SellerContextAsync(client);
        Authorize(client, sellerToken);
        ProductResponse product = await CreateDraftAsync(client, categoryId);

        ProductResponse restocked = (await (await client.PutAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/stock",
            new SetStockRequest(11))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(11, restocked.StockQuantity);

        ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants",
            new CreateProductVariantRequest("Small", "OWN-" + Guid.NewGuid().ToString("N")[..8], 70m, "EGP", 1)))
            .Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
        ProductVariantResponse variantStock = (await (await client.PutAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants/{variant.Id}/stock",
            new SetStockRequest(8))).Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
        Assert.Equal(8, variantStock.StockQuantity);

        AuthenticationResponse other = await RegisterAsync(client);
        string otherToken = await ApproveSellerAsync(client, other, adminToken);
        Authorize(client, otherToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/stock",
                new SetStockRequest(1))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants/{variant.Id}/stock",
                new SetStockRequest(1))).StatusCode);
    }

    [Fact]
    public async Task PurchaseInfo_UsesProductStockAndNullSku_WithoutVariants()
    {
        HttpClient client = _factory.CreateMigratedClient();
        ProductResponse product = await PublishAsync(client, withVariant: false, stock: 7);

        using IServiceScope scope = _factory.Services.CreateScope();
        IProductPurchaseQuery query = scope.ServiceProvider.GetRequiredService<IProductPurchaseQuery>();
        ProductPurchaseInfo info = (await query.GetManyForCartAsync(
            [new ProductPurchaseKey(product.Id, null)]))[0];
        Assert.Null(info.Sku);
        Assert.Equal(7, info.AvailableStock);
        Assert.Equal(product.Id, info.ProductId);
    }

    [Fact]
    public async Task PurchaseInfo_UsesVariantStockAndSku()
    {
        HttpClient client = _factory.CreateMigratedClient();
        ProductResponse product = await PublishAsync(client, withVariant: true, stock: 5);
        ProductVariantResponse variant = product.Variants[0];

        using IServiceScope scope = _factory.Services.CreateScope();
        IProductPurchaseQuery query = scope.ServiceProvider.GetRequiredService<IProductPurchaseQuery>();
        ProductPurchaseInfo info = (await query.GetManyForCartAsync(
            [new ProductPurchaseKey(product.Id, variant.Id)]))[0];
        Assert.Equal(variant.Sku, info.Sku);
        Assert.Equal(5, info.AvailableStock);
    }

    private async Task<ProductResponse> PublishAsync(HttpClient client, bool withVariant, int stock)
    {
        (Guid categoryId, string sellerToken, string adminToken) = await SellerContextAsync(client);
        Authorize(client, sellerToken);
        ProductResponse product = await CreateDraftAsync(client, categoryId, stock);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("products/stock.jpg", "https://cdn.local/stock.jpg", 1, true))).StatusCode);
        if (withVariant)
        {
            await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "PUB-" + Guid.NewGuid().ToString("N")[..8],
                    70m,
                    "EGP",
                    stock));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        Authorize(client, sellerToken);
        return (await (await client.GetAsync($"/api/v1/seller/products/{product.Id}")).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    private static async Task<ProductResponse> CreateDraftAsync(
        HttpClient client,
        Guid categoryId,
        int stockQuantity = 0)
    {
        return (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Stock Test Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                40m,
                "EGP",
                null,
                stockQuantity))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    private async Task<(Guid CategoryId, string SellerToken, string AdminToken)> SellerContextAsync(HttpClient client)
    {
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        return (categoryId, sellerToken, adminToken);
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
        string email = $"stk_{Guid.NewGuid():N}@example.com";
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
