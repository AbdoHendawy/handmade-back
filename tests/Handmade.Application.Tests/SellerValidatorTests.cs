using FluentValidation;
using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Application.Seller.Email;
using Handmade.Application.Seller.Validators;

namespace Handmade.Application.Tests;

public sealed class SellerValidatorTests
{
    [Fact]
    public async Task Submit_ValidRequest_Passes()
    {
        SubmitSellerApplicationRequestValidator validator = new();
        await validator.ValidateAndThrowAsync(ValidSubmit());
    }

    [Fact]
    public async Task Submit_EmptyBusinessName_Fails()
    {
        SubmitSellerApplicationRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(ValidSubmit() with { BusinessName = " " }));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SubmitSellerApplicationRequest.BusinessName));
    }

    [Fact]
    public async Task Submit_InvalidPhone_Fails()
    {
        SubmitSellerApplicationRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(ValidSubmit() with { Phone = "01000000001" }));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SubmitSellerApplicationRequest.Phone));
    }

    [Fact]
    public async Task Reject_ShortReason_Fails()
    {
        RejectSellerApplicationRequestValidator validator = new();
        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => validator.ValidateAndThrowAsync(new RejectSellerApplicationRequest("short")));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(RejectSellerApplicationRequest.Reason));
    }

    [Fact]
    public void PagingQuery_ClampsPageSize()
    {
        PagingQuery paging = new() { Page = 0, PageSize = 500 };
        Assert.Equal(PagingQuery.DefaultPage, paging.NormalizedPage);
        Assert.Equal(PagingQuery.MaxPageSize, paging.NormalizedPageSize);
        Assert.Equal(0, paging.Skip);
    }

    [Fact]
    public void ApprovedEmail_DoesNotIncludeSensitivePayload()
    {
        var message = SellerEmailTemplates.ApplicationApproved("user@example.com", "Abdo");
        Assert.Equal("Congratulations! Your Seller Account Is Approved", message.Subject);
        Assert.DoesNotContain("password", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private static SubmitSellerApplicationRequest ValidSubmit()
    {
        return new SubmitSellerApplicationRequest(
            "Abdo Handmade",
            "Handmade accessories and crafts studio.",
            "+201000000001");
    }
}
