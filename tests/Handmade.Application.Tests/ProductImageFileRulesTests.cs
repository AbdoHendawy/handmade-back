using Handmade.Application.Catalog;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;

namespace Handmade.Application.Tests;

public sealed class ProductImageFileRulesTests
{
    [Fact]
    public void Validate_JpegMagicAndType_ReturnsJpeg()
    {
        using MemoryStream stream = new(JpegHeader(32));
        Assert.Equal(ProductImageFileRules.Jpeg, ProductImageFileRules.Validate(stream, "image/jpeg", stream.Length));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Validate_MissingOrEmpty_ThrowsInvalidImageFile()
    {
        using MemoryStream stream = new();
        DomainException exception = Assert.Throws<DomainException>(
            () => ProductImageFileRules.Validate(stream, "image/jpeg", 0));
        Assert.Equal(CatalogErrorCodes.InvalidImageFile, exception.Code);
    }

    [Fact]
    public void Validate_Oversized_ThrowsImageTooLarge()
    {
        using MemoryStream stream = new(JpegHeader(64));
        DomainException exception = Assert.Throws<DomainException>(
            () => ProductImageFileRules.Validate(stream, "image/jpeg", ProductImageFileRules.MaxBytes + 1));
        Assert.Equal(CatalogErrorCodes.ImageTooLarge, exception.Code);
    }

    [Fact]
    public void Validate_UnsupportedContentType_Throws()
    {
        using MemoryStream stream = new(JpegHeader(32));
        DomainException exception = Assert.Throws<DomainException>(
            () => ProductImageFileRules.Validate(stream, "application/pdf", stream.Length));
        Assert.Equal(CatalogErrorCodes.ImageContentTypeNotAllowed, exception.Code);
    }

    [Fact]
    public void Validate_DeclaredTypeMismatch_Throws()
    {
        using MemoryStream stream = new(JpegHeader(32));
        DomainException exception = Assert.Throws<DomainException>(
            () => ProductImageFileRules.Validate(stream, "image/png", stream.Length));
        Assert.Equal(CatalogErrorCodes.ImageContentTypeNotAllowed, exception.Code);
    }

    [Fact]
    public void CreateStorageKey_UsesProductsPrefix()
    {
        string key = ProductImageFileRules.CreateStorageKey(ProductImageFileRules.Png);
        Assert.StartsWith("products/", key, StringComparison.Ordinal);
        Assert.EndsWith(".png", key, StringComparison.Ordinal);
    }

    private static byte[] JpegHeader(int size)
    {
        byte[] data = new byte[size];
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        return data;
    }
}
