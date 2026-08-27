using Handmade.Infrastructure.Storage;

namespace Handmade.Application.Tests;

public sealed class FileStorageOptionsTests
{
    [Fact]
    public void EmptyProvider_DoesNotRequireMinioSettings()
    {
        new FileStorageOptions().EnsureValidWhenEnabled();
    }

    [Fact]
    public void Minio_MissingSettings_Fails()
    {
        FileStorageOptions options = new() { Provider = "MinIO" };
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled);
    }

    [Fact]
    public void Minio_InvalidPublicBaseUrl_Fails()
    {
        FileStorageOptions options = ValidMinio();
        options.PublicBaseUrl = "not-a-url";
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled);
    }

    [Fact]
    public void Minio_ValidSettings_Pass()
    {
        ValidMinio().EnsureValidWhenEnabled();
    }

    [Fact]
    public void EmptyProvider_AllowedInDevelopment()
    {
        new FileStorageOptions().EnsureAllowedForEnvironment(isDevelopment: true);
    }

    [Fact]
    public void EmptyProvider_RejectedOutsideDevelopment()
    {
        Assert.Throws<InvalidOperationException>(
            () => new FileStorageOptions().EnsureAllowedForEnvironment(isDevelopment: false));
    }

    [Fact]
    public void Minio_AllowedOutsideDevelopment_WhenValid()
    {
        ValidMinio().EnsureAllowedForEnvironment(isDevelopment: false);
    }

    private static FileStorageOptions ValidMinio()
    {
        return new FileStorageOptions
        {
            Provider = "MinIO",
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            Bucket = "handmade",
            PublicBaseUrl = "http://localhost:9000/handmade"
        };
    }
}
