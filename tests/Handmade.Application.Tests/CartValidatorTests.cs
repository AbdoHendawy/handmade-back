using FluentValidation;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Cart.Validators;
using Handmade.Domain.Cart;

namespace Handmade.Application.Tests;

public sealed class CartValidatorTests
{
    [Fact]
    public async Task AddItem_ValidRequest_Passes()
    {
        AddCartItemRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(new AddCartItemRequest(Guid.CreateVersion7(), null, 2));
    }

    [Fact]
    public async Task AddItem_EmptyProductId_Fails()
    {
        AddCartItemRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new AddCartItemRequest(Guid.Empty, null, 1)));
        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(AddCartItemRequest.ProductId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CartLimits.MaxQuantityPerItem + 1)]
    public async Task AddItem_InvalidQuantity_Fails(int quantity)
    {
        AddCartItemRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new AddCartItemRequest(Guid.CreateVersion7(), null, quantity)));
        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(AddCartItemRequest.Quantity));
    }

    [Fact]
    public async Task UpdateItem_ValidQuantity_Passes()
    {
        UpdateCartItemRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(new UpdateCartItemRequest(5));
    }

    [Fact]
    public async Task UpdateItem_ZeroQuantity_Fails()
    {
        UpdateCartItemRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new UpdateCartItemRequest(0)));
        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(UpdateCartItemRequest.Quantity));
    }
}
