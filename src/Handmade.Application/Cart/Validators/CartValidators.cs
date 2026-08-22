using FluentValidation;
using Handmade.Application.Cart.DTOs;
using Handmade.Domain.Cart;

namespace Handmade.Application.Cart.Validators;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, CartLimits.MaxQuantityPerItem);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, CartLimits.MaxQuantityPerItem);
    }
}
