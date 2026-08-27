using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Catalog;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class ProductImageUploadEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public ProductImageUploadEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SellerUploadsValidImage_CreatesMetadata_AndSubmitApproveWorks()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, string sellerToken, string adminToken) = await CreateDraftProductAsync(client);
        Authorize(client, sellerToken);

        byte[] jpeg = MinimalJpeg();
        HttpResponseMessage uploaded = await client.PostAsync(
            $"/api/v1/seller/products/{product.Id}/images/upload",
            ImageForm(jpeg, "image/jpeg", "photo.jpg"));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        ProductImageResponse image = (await uploaded.Content.ReadFromJsonAsync<ProductImageResponse>(JsonOptions))!;
        Assert.False(string.IsNullOrWhiteSpace(image.StorageKey));
        Assert.StartsWith("products/", image.StorageKey, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(image.Url));
        Assert.Contains(image.StorageKey, image.Url, StringComparison.Ordinal);
        Assert.True(_factory.Files.Objects.ContainsKey(image.StorageKey));

        ProductResponse loaded = (await (await client.GetAsync($"/api/v1/seller/products/{product.Id}")).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Contains(loaded.Images, i => i.Id == image.Id && i.StorageKey == image.StorageKey);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);

        PublicProductResponse published = (await (await client.GetAsync($"/api/v1/catalog/products/{product.Slug}")).Content
            .ReadFromJsonAsync<PublicProductResponse>(JsonOptions))!;
        Assert.Contains(published.Images, i => i.Id == image.Id);
    }

    [Fact]
    public async Task ExistingMetadataImageEndpoint_StillWorks()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, string sellerToken, _) = await CreateDraftProductAsync(client);
        Authorize(client, sellerToken);

        HttpResponseMessage metadata = await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/images",
            new AddProductImageRequest("products/external.jpg", "https://cdn.local/external.jpg", 1, true));
        Assert.Equal(HttpStatusCode.Created, metadata.StatusCode);
        ProductImageResponse image = (await metadata.Content.ReadFromJsonAsync<ProductImageResponse>(JsonOptions))!;
        Assert.Equal("products/external.jpg", image.StorageKey);
        Assert.Equal("https://cdn.local/external.jpg", image.Url);
    }

    [Fact]
    public async Task CustomerCannotUpload_AndOtherSellerCannotUpload()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, _, string adminToken) = await CreateDraftProductAsync(client);

        AuthenticationResponse customer = await RegisterAsync(client, "cust");
        Authorize(client, customer.AccessToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync(
                $"/api/v1/seller/products/{product.Id}/images/upload",
                ImageForm(MinimalJpeg(), "image/jpeg", "photo.jpg"))).StatusCode);

        AuthenticationResponse other = await RegisterAsync(client, "other");
        string otherToken = await ApproveSellerAsync(client, other, adminToken);
        Authorize(client, otherToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync(
                $"/api/v1/seller/products/{product.Id}/images/upload",
                ImageForm(MinimalJpeg(), "image/jpeg", "photo.jpg"))).StatusCode);
    }

    [Fact]
    public async Task SuspendedSellerCannotUpload()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, string sellerToken, string adminToken) = await CreateDraftProductAsync(client);

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{product.SellerId}/suspend",
                new SuspendSellerRequest("Policy violation requires a temporary pause."))).StatusCode);

        Authorize(client, sellerToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync(
                $"/api/v1/seller/products/{product.Id}/images/upload",
                ImageForm(MinimalJpeg(), "image/jpeg", "photo.jpg"))).StatusCode);
    }

    [Fact]
    public async Task MissingFile_UnsupportedType_AndOversized_AreRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, string sellerToken, _) = await CreateDraftProductAsync(client);
        Authorize(client, sellerToken);
        string path = $"/api/v1/seller/products/{product.Id}/images/upload";

        using MultipartFormDataContent missing = new();
        missing.Add(new StringContent("true"), "isPrimary");
        HttpResponseMessage missingResponse = await client.PostAsync(path, missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        HttpResponseMessage typeResponse = await client.PostAsync(
            path,
            ImageForm("not-an-image"u8.ToArray(), "application/octet-stream", "payload.exe"));
        Assert.Equal(HttpStatusCode.BadRequest, typeResponse.StatusCode);

        byte[] oversized = new byte[ProductImageFileRules.MaxBytes + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;
        HttpResponseMessage sizeResponse = await client.PostAsync(path, ImageForm(oversized, "image/jpeg", "huge.jpg"));
        Assert.Equal(HttpStatusCode.BadRequest, sizeResponse.StatusCode);
    }

    [Fact]
    public async Task PendingReviewProduct_RejectsUpload()
    {
        HttpClient client = _factory.CreateMigratedClient();
        (ProductResponse product, string sellerToken, _) = await CreateDraftProductAsync(client);
        Authorize(client, sellerToken);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/v1/seller/products/{product.Id}/images",
            new AddProductImageRequest("products/one.jpg", null, 1, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);

        HttpResponseMessage upload = await client.PostAsync(
            $"/api/v1/seller/products/{product.Id}/images/upload",
            ImageForm(MinimalJpeg(), "image/jpeg", "two.jpg"));
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
    }

    private async Task<(ProductResponse Product, string SellerToken, string AdminToken)> CreateDraftProductAsync(
        HttpClient client)
    {
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client, "up");
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);
        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Uploaded Vase",
                "Handmade ceramic vase with a gloss glaze finish.",
                categoryId,
                120m,
                "EGP",
                null))).Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        return (product, sellerToken, adminToken);
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string contentType, string fileName)
    {
        ByteArrayContent file = new(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        MultipartFormDataContent form = new();
        form.Add(file, "file", fileName);
        return form;
    }

    private static byte[] MinimalJpeg()
    {
        byte[] data = new byte[32];
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        return data;
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
        AuthenticationResponse registered = await RegisterAsync(client, "adm");
        await _factory.AssignRoleAsync(registered.User.Id, RoleNames.Admin);
        AuthenticationResponse admin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(registered.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        return (admin, admin.AccessToken);
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client, string prefix)
    {
        string email = $"{prefix}_{Guid.NewGuid():N}@example.com";
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
