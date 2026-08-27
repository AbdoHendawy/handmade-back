using Handmade.Application.Abstractions.Storage;
using Handmade.Application.Catalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Handmade.Infrastructure.Storage;

public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly FileStorageOptions _options;
    private readonly ILogger<MinioFileStorage> _logger;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    public MinioFileStorage(IOptions<FileStorageOptions> options, ILogger<MinioFileStorage> logger)
    {
        _options = options.Value;
        _options.EnsureValidWhenEnabled();
        _logger = logger;
        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSsl)
            .Build();
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await EnsureBucketAsync(cancellationToken);

        Stream payload = content;
        if (!content.CanSeek)
        {
            MemoryStream copy = new();
            await content.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            payload = copy;
        }

        string key = string.IsNullOrWhiteSpace(fileName)
            ? ProductImageFileRules.CreateStorageKey(contentType)
            : fileName.Trim();

        await _client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_options.Bucket)
                .WithObject(key)
                .WithStreamData(payload)
                .WithObjectSize(payload.Length)
                .WithContentType(ProductImageFileRules.NormalizeContentType(contentType)),
            cancellationToken);

        return key;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        await EnsureBucketAsync(cancellationToken);
        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(storageKey),
                cancellationToken);
        }
        catch (MinioException exception)
        {
            _logger.LogWarning(exception, "Failed to delete storage object {StorageKey}", storageKey);
        }
    }

    public Task<Uri> GetUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new InvalidOperationException("Storage key is required.");
        }

        string baseUrl = _options.PublicBaseUrl.TrimEnd('/') + "/";
        return Task.FromResult(new Uri(new Uri(baseUrl, UriKind.Absolute), storageKey));
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            bool exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.Bucket),
                cancellationToken);
            if (!exists)
            {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_options.Bucket),
                    cancellationToken);
            }

            try
            {
                string policy = $$"""
                    {
                      "Version": "2012-10-17",
                      "Statement": [
                        {
                          "Effect": "Allow",
                          "Principal": { "AWS": ["*"] },
                          "Action": ["s3:GetObject"],
                          "Resource": ["arn:aws:s3:::{{_options.Bucket}}/*"]
                        }
                      ]
                    }
                    """;
                await _client.SetPolicyAsync(
                    new SetPolicyArgs()
                        .WithBucket(_options.Bucket)
                        .WithPolicy(policy),
                    cancellationToken);
            }
            catch (MinioException exception)
            {
                _logger.LogWarning(exception, "Could not set public-read policy on bucket {Bucket}", _options.Bucket);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }
}
