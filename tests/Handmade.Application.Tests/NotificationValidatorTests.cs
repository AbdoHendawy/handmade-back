using FluentValidation;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Validators;

namespace Handmade.Application.Tests;

public sealed class NotificationValidatorTests
{
    [Fact]
    public async Task AdminCreate_ValidUserRequest_Passes()
    {
        AdminCreateNotificationRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(new AdminCreateNotificationRequest(
            "admin.broadcast",
            "Notice",
            "Hello",
            Guid.CreateVersion7()));
    }

    [Fact]
    public async Task AdminCreate_RequiresUserOrRole_NotBoth()
    {
        AdminCreateNotificationRequestValidator validator = new();
        ValidationException neither = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new AdminCreateNotificationRequest("type", "Title", "Body")));
        Assert.Contains(neither.Errors, e => e.PropertyName == "UserId");

        ValidationException both = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new AdminCreateNotificationRequest(
                "type",
                "Title",
                "Body",
                Guid.CreateVersion7(),
                "Admin")));
        Assert.Contains(both.Errors, e => e.PropertyName == "UserId");
    }
}
