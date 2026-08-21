namespace Handmade.Application.Abstractions.Storage;

/// <summary>
/// Object-storage abstraction for artwork images and other binary assets.
/// Binary data must not be stored in PostgreSQL.
/// Implementations (S3, R2, Azure Blob, MinIO) live in Infrastructure.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads content and returns a storage key (not a public URL).
    /// </summary>
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a time-limited or CDN URL for clients to fetch the object.
    /// </summary>
    Task<Uri> GetUrlAsync(string storageKey, CancellationToken cancellationToken = default);
}
