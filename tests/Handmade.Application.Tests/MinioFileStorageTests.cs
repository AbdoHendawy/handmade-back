using Handmade.Application.Catalog;
using Handmade.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.Minio;

namespace Handmade.Application.Tests;

[Collection(nameof(MinioStorageCollection))]
public sealed class MinioFileStorageTests
{
    private readonly MinioStorageFixture _fixture;

    public MinioFileStorageTests(MinioStorageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Upload_StoresObject_AndReturnsConfiguredUrl()
    {
        MinioFileStorage storage = CreateStorage();
        string key = ProductImageFileRules.CreateStorageKey(ProductImageFileRules.Jpeg);
        await using MemoryStream content = new(JpegBytes());

        string stored = await storage.UploadAsync(content, key, ProductImageFileRules.Jpeg);
        Assert.Equal(key, stored);

        Uri url = await storage.GetUrlAsync(stored);
        Assert.Equal($"{_fixture.PublicBaseUrl}/{key}", url.ToString());

        StatObjectArgs stat = new StatObjectArgs()
            .WithBucket(MinioStorageFixture.Bucket)
            .WithObject(key);
        await _fixture.CreateClient().StatObjectAsync(stat);
    }

    [Fact]
    public async Task Upload_HonorsCancellation()
    {
        MinioFileStorage storage = CreateStorage();
        await using MemoryStream content = new(JpegBytes());
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.UploadAsync(content, "products/cancelled.jpg", ProductImageFileRules.Jpeg, cts.Token));
    }

    [Fact]
    public void Constructor_InvalidOptions_Fails()
    {
        IOptions<FileStorageOptions> options = Options.Create(new FileStorageOptions { Provider = "MinIO" });
        Assert.Throws<InvalidOperationException>(
            () => new MinioFileStorage(options, NullLogger<MinioFileStorage>.Instance));
    }

    private MinioFileStorage CreateStorage()
    {
        return new MinioFileStorage(Options.Create(_fixture.Options), NullLogger<MinioFileStorage>.Instance);
    }

    private static byte[] JpegBytes()
    {
        byte[] data = new byte[32];
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        return data;
    }
}

public sealed class MinioStorageFixture : IAsyncLifetime
{
    public const string Bucket = "handmade-tests";
    public const string AccessKey = "minio";
    public const string SecretKey = "minio123";

    private readonly MinioContainer _container = new MinioBuilder("minio/minio:latest")
        .WithUsername(AccessKey)
        .WithPassword(SecretKey)
        .Build();

    public string PublicBaseUrl { get; private set; } = string.Empty;

    public FileStorageOptions Options { get; private set; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Uri endpoint = new(_container.GetConnectionString());
        string hostPort = $"{endpoint.Host}:{endpoint.Port}";
        PublicBaseUrl = $"http://{hostPort}/{Bucket}";
        Options = new FileStorageOptions
        {
            Provider = FileStorageOptions.MinioProvider,
            Endpoint = hostPort,
            UseSsl = false,
            AccessKey = _container.GetAccessKey(),
            SecretKey = _container.GetSecretKey(),
            Bucket = Bucket,
            PublicBaseUrl = PublicBaseUrl
        };
    }

    public IMinioClient CreateClient()
    {
        return new MinioClient()
            .WithEndpoint(Options.Endpoint)
            .WithCredentials(Options.AccessKey, Options.SecretKey)
            .WithSSL(false)
            .Build();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(MinioStorageCollection))]
public sealed class MinioStorageCollection : ICollectionFixture<MinioStorageFixture>;
