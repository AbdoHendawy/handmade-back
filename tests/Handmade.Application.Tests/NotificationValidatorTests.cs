using FluentValidation;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Validators;

namespace Handmade.Application.Tests;

public sealed class NotificationValidatorTests
{
    [Fact]
    public async Task CreateInbox_ValidRequest_Passes()
    {
        CreateInboxNotificationRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(ValidInboxCreate());
    }

    [Fact]
    public async Task CreateInbox_EmptyTitle_Fails()
    {
        CreateInboxNotificationRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(ValidInboxCreate() with { Title = " " }));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(CreateInboxNotificationRequest.Title));
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

    private static CreateInboxNotificationRequest ValidInboxCreate()
    {
        return new CreateInboxNotificationRequest("system.manual", "Hello", "Body text");
    }
}
