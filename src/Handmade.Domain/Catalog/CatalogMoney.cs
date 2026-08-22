using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Catalog;

/// <summary>
/// Decimal money helpers. Amounts are stored as <see cref="decimal"/> with two fractional digits.
/// Default marketplace currency is EGP until multi-currency checkout exists.
/// </summary>
public static class CatalogMoney
{
    public const string DefaultCurrency = "EGP";

    public const int Precision = 18;

    public const int Scale = 2;

    public static decimal RequireAmount(decimal amount)
    {
        if (amount < 0)
        {
            throw new DomainException("Price cannot be negative.") { Code = CatalogErrorCodes.InvalidPrice };
        }

        if (decimal.Round(amount, Scale, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainException("Price cannot have more than two decimal places.")
            {
                Code = CatalogErrorCodes.InvalidPrice
            };
        }

        return amount;
    }

    public static string RequireCurrency(string? currency)
    {
        string value = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (value.Length != 3 || !value.All(char.IsAsciiLetter))
        {
            throw new DomainException("Currency must be a 3-letter ISO 4217 code.")
            {
                Code = CatalogErrorCodes.InvalidCurrency
            };
        }

        return value;
    }
}
