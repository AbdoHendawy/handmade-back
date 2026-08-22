using FluentValidation;
using Handmade.Application.Catalog.DTOs;
using Handmade.Domain.Catalog;

namespace Handmade.Application.Catalog.Validators;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.Slug).MaximumLength(Category.SlugMaxLength).When(x => x.Slug is not null);
        RuleFor(x => x.Description).MaximumLength(Category.DescriptionMaxLength).When(x => x.Description is not null);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.Slug).MaximumLength(Category.SlugMaxLength).When(x => x.Slug is not null);
        RuleFor(x => x.Description).MaximumLength(Category.DescriptionMaxLength).When(x => x.Description is not null);
    }
}

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Product.DescriptionMaxLength);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
        RuleFor(x => x.Slug).MaximumLength(Product.SlugMaxLength).When(x => x.Slug is not null);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Product.DescriptionMaxLength);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
        RuleFor(x => x.Slug).MaximumLength(Product.SlugMaxLength).When(x => x.Slug is not null);
    }
}

public sealed class RejectProductRequestValidator : AbstractValidator<RejectProductRequest>
{
    public RejectProductRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(Product.RejectionReasonMinLength)
            .MaximumLength(Product.RejectionReasonMaxLength);
    }
}

public sealed class AddProductImageRequestValidator : AbstractValidator<AddProductImageRequest>
{
    public AddProductImageRequestValidator()
    {
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(ProductImage.StorageKeyMaxLength);
        RuleFor(x => x.Url).MaximumLength(ProductImage.UrlMaxLength).When(x => x.Url is not null);
        RuleFor(x => x.SortOrder).GreaterThan(0).When(x => x.SortOrder.HasValue);
    }
}

public sealed class ReorderProductImagesRequestValidator : AbstractValidator<ReorderProductImagesRequest>
{
    public ReorderProductImagesRequestValidator()
    {
        RuleFor(x => x.ImageIds).NotNull().NotEmpty();
    }
}

public sealed class CreateProductVariantRequestValidator : AbstractValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ProductVariant.NameMaxLength);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(ProductVariant.SkuMaxLength);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
    }
}

public sealed class UpdateProductVariantRequestValidator : AbstractValidator<UpdateProductVariantRequest>
{
    public UpdateProductVariantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ProductVariant.NameMaxLength);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(ProductVariant.SkuMaxLength);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
    }
}
