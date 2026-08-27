using System.Collections.Concurrent;
using Handmade.Application.Abstractions.Storage;
using Handmade.Application.Catalog;

namespace Handmade.Api.Tests;

public sealed class FakeFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public const string PublicBaseUrl = "http://files.test/handmade";

    public IReadOnlyDictionary<string, byte[]> Objects => _objects;

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        string key = string.IsNullOrWhiteSpace(fileName)
            ? ProductImageFileRules.CreateStorageKey(contentType)
            : fileName.Trim();

        using MemoryStream copy = new();
        await content.CopyToAsync(copy, cancellationToken);
        _objects[key] = copy.ToArray();
        return key;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            _objects.TryRemove(storageKey, out _);
        }

        return Task.CompletedTask;
    }

    public Task<Uri> GetUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Uri($"{PublicBaseUrl.TrimEnd('/')}/{storageKey.TrimStart('/')}"));
    }
}
