using FluentValidation;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Orders.Validators;

namespace Handmade.Application.Tests;

public sealed class CheckoutValidatorTests
{
    [Fact]
    public async Task ValidDelivery_Passes()
    {
        CheckoutRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(Valid());
    }

    [Fact]
    public async Task MissingRecipient_Fails()
    {
        CheckoutRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(Valid() with { RecipientName = "" }));
        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(CheckoutRequest.RecipientName));
    }

    [Fact]
    public async Task MissingCity_Fails()
    {
        CheckoutRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(Valid() with { City = "  " }));
        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(CheckoutRequest.City));
    }

    private static CheckoutRequest Valid()
    {
        return new CheckoutRequest(
            "Nour Hassan",
            "+201001234567",
            "12 Nile Street",
            "Apt 4",
            "Cairo",
            "Cairo",
            "11511",
            "Leave at the door");
    }
}
