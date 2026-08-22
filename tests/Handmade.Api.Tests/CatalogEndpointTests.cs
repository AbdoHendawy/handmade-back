using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class CatalogEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public CatalogEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomerCannotCreateProduct_AndSellerCannotManageCategories()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest("Item", "A handmade product description.", Guid.CreateVersion7(), 10m, "EGP", null))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest("Hijack", "hijack", null, null))).StatusCode);
    }

    [Fact]
    public async Task SellerLifecycle_PublicCatalogHidesUnpublished_AndOwnershipIsEnforced()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);

        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Handmade Leather Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                250m,
                "EGP",
                null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ProductResponse product = (await created.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(nameof(ProductStatus.Draft), product.Status);
        Assert.Equal(product.SellerId, product.Seller.Id);
        Assert.NotEqual(sellerUser.User.Id, product.SellerId);

        HttpResponseMessage image = await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/images",
            new AddProductImageRequest("products/bracelet.jpg", "https://cdn.local/bracelet.jpg", 1, true));
        Assert.Equal(HttpStatusCode.Created, image.StatusCode);

        HttpResponseMessage variant = await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants",
            new CreateProductVariantRequest("Small", "BRC-SML", 240m, "EGP"));
        Assert.Equal(HttpStatusCode.Created, variant.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/variants",
            new CreateProductVariantRequest("Medium", "BRC-SML", 260m, "EGP"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);

        PagedResult<PublicProductResponse> hidden = (await (await client.GetAsync("/api/v1/catalog/products")).Content
            .ReadFromJsonAsync<PagedResult<PublicProductResponse>>(JsonOptions))!;
        Assert.DoesNotContain(hidden.Items, p => p.Id == product.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).StatusCode);

        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);

        PublicProductResponse published = (await (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).Content
            .ReadFromJsonAsync<PublicProductResponse>(JsonOptions))!;
        Assert.Equal(product.Id, published.Id);
        Assert.Equal("Handmade Leather Bracelet", published.Name);
        Assert.DoesNotContain("status", await (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).Content.ReadAsStringAsync());

        AuthenticationResponse other = await RegisterAsync(client);
        string otherToken = await ApproveSellerAsync(client, other, adminToken);
        Authorize(client, otherToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/seller/products/{product.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}",
                new UpdateProductRequest(
                    "Stolen",
                    "Handmade leather bracelet with a brass clasp.",
                    categoryId,
                    1m,
                    "EGP",
                    null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("x.jpg", null, 2, false))).StatusCode);

        Authorize(client, sellerToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).StatusCode);
    }

    [Fact]
    public async Task InactiveCategory_CannotBeUsedForNewProducts()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Authorize(client, adminToken);
        CategoryResponse created = (await (await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest("Seasonal", null, null, null))).Content
            .ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/categories/{created.Id}/deactivate", null)).StatusCode);

        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Seasonal Bowl",
                "Handmade ceramic bowl for the seasonal collection.",
                created.Id,
                80m,
                "EGP",
                null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CircularCategory_IsRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Authorize(client, adminToken);

        CategoryResponse parent = (await (await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest("Woodwork", null, null, null))).Content
            .ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
        CategoryResponse child = (await (await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest("Carving", null, null, parent.Id))).Content
            .ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;

        HttpResponseMessage cycle = await client.PutAsJsonAsync(
            $"/api/v1/admin/categories/{parent.Id}",
            new UpdateCategoryRequest("Woodwork", parent.Slug, null, child.Id));
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
    }

    [Fact]
    public async Task AdminCanReject_AndPublicCatalogStaysHidden()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);

        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Clay Bowl",
                "Handmade clay bowl with a matte glaze finish.",
                categoryId,
                70m,
                "EGP",
                null))).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/images",
            new AddProductImageRequest("products/bowl.jpg", null, 1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/products/{product.Id}/reject",
                new RejectProductRequest("Product images do not meet marketplace requirements."))).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).StatusCode);

        Authorize(client, sellerToken);
        ProductResponse rejected = (await (await client.GetAsync($"/api/v1/seller/products/{product.Id}")).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(nameof(ProductStatus.Rejected), rejected.Status);
        Assert.Contains("marketplace", rejected.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SellerCannotApprove_AndDraftDeleteWorks()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);

        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Clay Vase",
                "Handmade clay vase with a matte glaze finish.",
                categoryId,
                90m,
                "EGP",
                null))).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/seller/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/seller/products/{product.Id}")).StatusCode);
        _ = adminToken;
    }

    [Fact]
    public async Task InvalidSort_IsRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog/products?sort=price;drop table");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        string email = $"cat_{Guid.NewGuid():N}@example.com";
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
