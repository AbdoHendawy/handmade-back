namespace Handmade.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public const string MinioProvider = "MinIO";

    public string Provider { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsMinio => string.Equals(Provider, MinioProvider, StringComparison.OrdinalIgnoreCase);

    public void EnsureValidWhenEnabled()
    {
        if (!IsMinio)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Endpoint)
            || string.IsNullOrWhiteSpace(AccessKey)
            || string.IsNullOrWhiteSpace(SecretKey)
            || string.IsNullOrWhiteSpace(Bucket)
            || string.IsNullOrWhiteSpace(PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "FileStorage MinIO requires Endpoint, AccessKey, SecretKey, Bucket, and PublicBaseUrl.");
        }

        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("FileStorage:PublicBaseUrl must be an absolute URL.");
        }
    }
}
