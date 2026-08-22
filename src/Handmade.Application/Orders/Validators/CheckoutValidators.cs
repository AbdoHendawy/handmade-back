using FluentValidation;
using Handmade.Application.Orders.DTOs;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Application.Orders.Validators;

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty()
            .MaximumLength(OrderDeliverySnapshot.RecipientNameMaxLength);
        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(OrderDeliverySnapshot.PhoneMaxLength);
        RuleFor(x => x.AddressLine1)
            .NotEmpty()
            .MaximumLength(OrderDeliverySnapshot.AddressLineMaxLength);
        RuleFor(x => x.AddressLine2)
            .MaximumLength(OrderDeliverySnapshot.AddressLineMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.AddressLine2));
        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(OrderDeliverySnapshot.CityMaxLength);
        RuleFor(x => x.Governorate)
            .NotEmpty()
            .MaximumLength(OrderDeliverySnapshot.GovernorateMaxLength);
        RuleFor(x => x.PostalCode)
            .MaximumLength(OrderDeliverySnapshot.PostalCodeMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.PostalCode));
        RuleFor(x => x.Notes)
            .MaximumLength(OrderDeliverySnapshot.NotesMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
