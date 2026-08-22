using FluentValidation;
using Handmade.Application.Seller.DTOs;

namespace Handmade.Application.Seller.Validators;

public sealed class SubmitSellerApplicationRequestValidator : AbstractValidator<SubmitSellerApplicationRequest>
{
    public SubmitSellerApplicationRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MinimumLength(SellerLimits.BusinessNameMinLength)
            .MaximumLength(SellerLimits.BusinessNameMaxLength);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(SellerLimits.DescriptionMinLength)
            .MaximumLength(SellerLimits.DescriptionMaxLength);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(SellerLimits.PhoneMaxLength)
            .Matches(@"^\+[1-9]\d{7,14}$")
            .WithMessage("Phone must be an E.164 number, for example +201000000001.");
    }
}

public sealed class UpdateSellerProfileRequestValidator : AbstractValidator<UpdateSellerProfileRequest>
{
    public UpdateSellerProfileRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MinimumLength(SellerLimits.BusinessNameMinLength)
            .MaximumLength(SellerLimits.BusinessNameMaxLength);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(SellerLimits.DescriptionMinLength)
            .MaximumLength(SellerLimits.DescriptionMaxLength);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(SellerLimits.PhoneMaxLength)
            .Matches(@"^\+[1-9]\d{7,14}$")
            .WithMessage("Phone must be an E.164 number, for example +201000000001.");
    }
}

public sealed class RejectSellerApplicationRequestValidator : AbstractValidator<RejectSellerApplicationRequest>
{
    public RejectSellerApplicationRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(SellerLimits.ReasonMinLength)
            .MaximumLength(SellerLimits.ReasonMaxLength);
    }
}

public sealed class SuspendSellerRequestValidator : AbstractValidator<SuspendSellerRequest>
{
    public SuspendSellerRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(SellerLimits.ReasonMinLength)
            .MaximumLength(SellerLimits.ReasonMaxLength);
    }
}
