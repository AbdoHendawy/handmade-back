using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Orders.ValueObjects;

public sealed class OrderDeliverySnapshot
{
    public const int RecipientNameMaxLength = 200;
    public const int PhoneMaxLength = 32;
    public const int AddressLineMaxLength = 300;
    public const int CityMaxLength = 100;
    public const int GovernorateMaxLength = 100;
    public const int PostalCodeMaxLength = 16;
    public const int NotesMaxLength = 1000;

    private OrderDeliverySnapshot(
        string recipientName,
        string phone,
        string addressLine1,
        string? addressLine2,
        string city,
        string governorate,
        string? postalCode,
        string? notes)
    {
        RecipientName = recipientName;
        Phone = phone;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        Governorate = governorate;
        PostalCode = postalCode;
        Notes = notes;
    }

    public string RecipientName { get; }

    public string Phone { get; }

    public string AddressLine1 { get; }

    public string? AddressLine2 { get; }

    public string City { get; }

    public string Governorate { get; }

    public string? PostalCode { get; }

    public string? Notes { get; }

    public static OrderDeliverySnapshot Create(
        string recipientName,
        string phone,
        string addressLine1,
        string? addressLine2,
        string city,
        string governorate,
        string? postalCode,
        string? notes)
    {
        return new OrderDeliverySnapshot(
            RequireText(recipientName, RecipientNameMaxLength, "Recipient name is required."),
            RequireText(phone, PhoneMaxLength, "Phone is required."),
            RequireText(addressLine1, AddressLineMaxLength, "Address line 1 is required."),
            OptionalText(addressLine2, AddressLineMaxLength, "Address line 2 is too long."),
            RequireText(city, CityMaxLength, "City is required."),
            RequireText(governorate, GovernorateMaxLength, "Governorate is required."),
            OptionalText(postalCode, PostalCodeMaxLength, "Postal code is too long."),
            OptionalText(notes, NotesMaxLength, "Delivery notes are too long."));
    }

    private static string RequireText(string? value, int maxLength, string message)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 || trimmed.Length > maxLength)
        {
            throw new DomainException(message) { Code = "invalid_delivery" };
        }

        return trimmed;
    }

    private static string? OptionalText(string? value, int maxLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException(message) { Code = "invalid_delivery" };
        }

        return trimmed;
    }
}
