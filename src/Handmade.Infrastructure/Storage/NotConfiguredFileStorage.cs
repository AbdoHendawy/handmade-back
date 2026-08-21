using Handmade.Application.Abstractions.Storage;

namespace Handmade.Infrastructure.Storage;

/// <summary>
/// Placeholder storage provider. Replace with S3/R2/Azure/MinIO in a later sprint.
/// </summary>
public sealed class NotConfiguredFileStorage : IFileStorage
{
    private static readonly InvalidOperationException NotConfigured = new(
        "File storage is not configured. Register a real IFileStorage implementation (S3, R2, Azure Blob, or MinIO) before uploading files.");

    public Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
        => throw NotConfigured;

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        => throw NotConfigured;

    public Task<Uri> GetUrlAsync(string storageKey, CancellationToken cancellationToken = default)
        => throw NotConfigured;
}
