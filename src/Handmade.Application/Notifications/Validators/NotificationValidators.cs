using FluentValidation;
using Handmade.Application.Notifications.DTOs;

// NotificationLimits lives in the parent namespace.

namespace Handmade.Application.Notifications.Validators;

public sealed class CreateInboxNotificationRequestValidator : AbstractValidator<CreateInboxNotificationRequest>
{
    public CreateInboxNotificationRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(NotificationLimits.TypeMaxLength);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(NotificationLimits.TitleMaxLength);

        RuleFor(x => x.Body)
            .MaximumLength(NotificationLimits.BodyMaxLength);

        RuleFor(x => x.DataJson)
            .MaximumLength(NotificationLimits.DataJsonMaxLength)
            .When(x => x.DataJson is not null);

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(NotificationLimits.IdempotencyKeyMaxLength)
            .When(x => x.IdempotencyKey is not null);
    }
}

public sealed class UpdateNotificationRequestValidator : AbstractValidator<UpdateNotificationRequest>
{
    public UpdateNotificationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(NotificationLimits.TitleMaxLength);

        RuleFor(x => x.Body)
            .MaximumLength(NotificationLimits.BodyMaxLength);

        RuleFor(x => x.DataJson)
            .MaximumLength(NotificationLimits.DataJsonMaxLength)
            .When(x => x.DataJson is not null);
    }
}

public sealed class AdminCreateNotificationRequestValidator : AbstractValidator<AdminCreateNotificationRequest>
{
    public AdminCreateNotificationRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(NotificationLimits.TypeMaxLength);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(NotificationLimits.TitleMaxLength);

        RuleFor(x => x.Body)
            .MaximumLength(NotificationLimits.BodyMaxLength);

        RuleFor(x => x.DataJson)
            .MaximumLength(NotificationLimits.DataJsonMaxLength)
            .When(x => x.DataJson is not null);

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(NotificationLimits.IdempotencyKeyMaxLength)
            .When(x => x.IdempotencyKey is not null);

        RuleFor(x => x.RoleName)
            .MaximumLength(64)
            .When(x => x.RoleName is not null);

        RuleFor(x => x)
            .Must(x => x.UserId.HasValue ^ !string.IsNullOrWhiteSpace(x.RoleName))
            .WithMessage("Provide either userId or roleName, not both.")
            .WithName("UserId");
    }
}
