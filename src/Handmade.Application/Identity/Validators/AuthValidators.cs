using FluentValidation;
using Handmade.Application.Identity.DTOs;

namespace Handmade.Application.Identity.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(IdentityLimits.EmailMaxLength);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(IdentityLimits.PasswordMinLength)
            .MaximumLength(IdentityLimits.PasswordMaxLength)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(IdentityLimits.NameMaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(IdentityLimits.NameMaxLength);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(IdentityLimits.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
