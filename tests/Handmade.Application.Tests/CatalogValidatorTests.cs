using FluentValidation;
using Handmade.Application.Catalog;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Validators;

namespace Handmade.Application.Tests;

public sealed class CatalogValidatorTests
{
    [Fact]
    public async Task CreateProduct_ValidRequest_Passes()
    {
        CreateProductRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(ValidProduct());
    }

    [Fact]
    public async Task CreateProduct_NegativePrice_Fails()
    {
        CreateProductRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(ValidProduct() with { Price = -1m }));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(CreateProductRequest.Price));
    }

    [Fact]
    public async Task CreateProduct_NegativeStock_Fails()
    {
        CreateProductRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(ValidProduct() with { StockQuantity = -1 }));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(CreateProductRequest.StockQuantity));
    }

    [Fact]
    public async Task SetStock_Negative_Fails()
    {
        SetStockRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new SetStockRequest(-4)));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SetStockRequest.StockQuantity));
    }

    [Fact]
    public async Task RejectProduct_ShortReason_Fails()
    {
        RejectProductRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new RejectProductRequest("too short")));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(RejectProductRequest.Reason));
    }

    [Fact]
    public async Task AddImage_EmptyStorageKey_Fails()
    {
        AddProductImageRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new AddProductImageRequest(" ", null, 1, true)));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(AddProductImageRequest.StorageKey));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("newest", true)]
    [InlineData("priceAsc", true)]
    [InlineData("priceDesc", true)]
    [InlineData("price;drop table", false)]
    [InlineData("Name", false)]
    public void SortWhitelist_OnlyAllowsKnownValues(string? sort, bool allowed)
    {
        Assert.Equal(allowed, CatalogSortOptions.IsAllowed(sort));
    }

    private static CreateProductRequest ValidProduct()
    {
        return new CreateProductRequest(
            "Handmade Leather Bracelet",
            "Handmade leather bracelet with a brass clasp.",
            Guid.CreateVersion7(),
            250m,
            "EGP",
            null);
    }
}
