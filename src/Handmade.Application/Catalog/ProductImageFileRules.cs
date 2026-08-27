using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;

namespace Handmade.Application.Catalog;

/// <summary>
/// Shared image-upload limits and content checks. Does not trust client filenames.
/// </summary>
public static class ProductImageFileRules
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public const int MaxRequestBytes = MaxBytes + (256 * 1024);

    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Gif = "image/gif";

    public static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }

    public static string Validate(Stream content, string? declaredContentType, long length)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (length <= 0)
        {
            throw new DomainException("An image file is required.") { Code = CatalogErrorCodes.InvalidImageFile };
        }

        if (length > MaxBytes)
        {
            throw new DomainException($"Image files must be {MaxBytes} bytes or smaller.")
            {
                Code = CatalogErrorCodes.ImageTooLarge
            };
        }

        string declared = NormalizeContentType(declaredContentType);
        if (declared is "image/jpg")
        {
            declared = Jpeg;
        }

        if (!IsAllowedContentType(declared))
        {
            throw new DomainException("Only JPEG, PNG, WebP, and GIF images are allowed.")
            {
                Code = CatalogErrorCodes.ImageContentTypeNotAllowed
            };
        }

        string detected = DetectContentType(content);
        if (!string.Equals(detected, declared, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("The file contents do not match the declared image type.")
            {
                Code = CatalogErrorCodes.ImageContentTypeNotAllowed
            };
        }

        return detected;
    }

    public static string CreateStorageKey(string contentType)
    {
        string extension = NormalizeContentType(contentType) switch
        {
            Jpeg or "image/jpg" => ".jpg",
            Png => ".png",
            Webp => ".webp",
            Gif => ".gif",
            _ => ".bin"
        };

        return $"products/{Guid.CreateVersion7():N}{extension}";
    }

    public static bool IsAllowedContentType(string contentType)
    {
        string normalized = NormalizeContentType(contentType);
        return normalized is Jpeg or "image/jpg" or Png or Webp or Gif;
    }

    private static string DetectContentType(Stream content)
    {
        if (!content.CanSeek)
        {
            throw new DomainException("Image content must be readable.") { Code = CatalogErrorCodes.InvalidImageFile };
        }

        long position = content.Position;
        Span<byte> header = stackalloc byte[12];
        int read = content.Read(header);
        content.Position = position;

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return Jpeg;
        }

        if (read >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return Png;
        }

        if (read >= 12
            && header[0] == (byte)'R'
            && header[1] == (byte)'I'
            && header[2] == (byte)'F'
            && header[3] == (byte)'F'
            && header[8] == (byte)'W'
            && header[9] == (byte)'E'
            && header[10] == (byte)'B'
            && header[11] == (byte)'P')
        {
            return Webp;
        }

        if (read >= 6
            && header[0] == (byte)'G'
            && header[1] == (byte)'I'
            && header[2] == (byte)'F'
            && header[3] == (byte)'8'
            && (header[4] == (byte)'7' || header[4] == (byte)'9')
            && header[5] == (byte)'a')
        {
            return Gif;
        }

        throw new DomainException("The file is not a recognized image.") { Code = CatalogErrorCodes.InvalidImageFile };
    }
}
