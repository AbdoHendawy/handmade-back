using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

public static class CatalogSlug
{
    private static readonly Regex NonSlug = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string FromName(string name)
    {
        string source = (name ?? string.Empty).Trim().ToLowerInvariant();
        string normalized = source.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);
        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        string ascii = builder.ToString().Normalize(NormalizationForm.FormC);
        string slug = NonSlug.Replace(ascii, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }

    public static string Require(string slug)
    {
        string trimmed = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 220 || trimmed.Contains('/') || trimmed.Contains(' '))
        {
            throw new DomainException("Slug is required and must be URL-safe.") { Code = CatalogErrorCodes.InvalidSlug };
        }

        return trimmed;
    }

    public static string NextUnique(string baseSlug, IReadOnlySet<string> existing)
    {
        string candidate = Require(baseSlug);
        if (!existing.Contains(candidate))
        {
            return candidate;
        }

        for (int i = 2; i < 10_000; i++)
        {
            string next = $"{candidate}-{i}";
            if (!existing.Contains(next))
            {
                return next;
            }
        }

        throw new ConflictException("Unable to allocate a unique slug.") { Code = CatalogErrorCodes.DuplicateSlug };
    }
}
