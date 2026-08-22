using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Handmade.Domain.Seller.Events;

namespace Handmade.Domain.Tests;

public sealed class SellerApplicationTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Submit_CreatesPendingApplication_AndRaisesEvent()
    {
        SellerApplication application = SellerApplication.Submit(
            UserId,
            "  Abdo Handmade  ",
            "Handmade accessories and crafts studio.",
            "+201000000001");

        Assert.Equal(SellerApplicationStatus.Pending, application.Status);
        Assert.Equal("Abdo Handmade", application.BusinessName);
        Assert.Equal(UserId, application.UserId);
        Assert.Contains(application.DomainEvents, e => e is SellerApplicationSubmitted submitted
            && submitted.ApplicationId == application.Id
            && submitted.UserId == UserId);
    }

    [Fact]
    public void Submit_EmptyBusinessName_Throws()
    {
        DomainException exception = Assert.Throws<DomainException>(() =>
            SellerApplication.Submit(UserId, "  ", "Handmade accessories and crafts studio.", "+201000000001"));

        Assert.Equal(SellerErrorCodes.InvalidBusinessName, exception.Code);
    }

    [Fact]
    public void Approve_Pending_SetsReviewFields()
    {
        SellerApplication application = CreatePending();

        application.Approve(AdminId, Now);

        Assert.Equal(SellerApplicationStatus.Approved, application.Status);
        Assert.Equal(AdminId, application.ReviewedBy);
        Assert.Equal(Now, application.ReviewedAt);
        Assert.Null(application.RejectionReason);
    }

    [Fact]
    public void Approve_OwnApplication_ThrowsForbidden()
    {
        SellerApplication application = CreatePending();

        ForbiddenException exception = Assert.Throws<ForbiddenException>(() =>
            application.Approve(UserId, Now));

        Assert.Equal(SellerErrorCodes.CannotApproveOwnApplication, exception.Code);
        Assert.Equal(SellerApplicationStatus.Pending, application.Status);
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsConflict()
    {
        SellerApplication application = CreatePending();
        application.Approve(AdminId, Now);

        ConflictException exception = Assert.Throws<ConflictException>(() =>
            application.Approve(AdminId, Now));

        Assert.Equal(SellerErrorCodes.ApplicationNotPending, exception.Code);
    }

    [Fact]
    public void Reject_Pending_StoresReasonAndRaisesEvent()
    {
        SellerApplication application = CreatePending();

        application.Reject(AdminId, "Please provide a more detailed business description.", Now);

        Assert.Equal(SellerApplicationStatus.Rejected, application.Status);
        Assert.Equal("Please provide a more detailed business description.", application.RejectionReason);
        Assert.Contains(application.DomainEvents, e => e is SellerApplicationRejected rejected
            && rejected.ApplicationId == application.Id
            && rejected.RejectedBy == AdminId);
    }

    [Fact]
    public void Reject_EmptyReason_Throws()
    {
        SellerApplication application = CreatePending();

        DomainException exception = Assert.Throws<DomainException>(() =>
            application.Reject(AdminId, "  ", Now));

        Assert.Equal(SellerErrorCodes.RejectionReasonRequired, exception.Code);
    }

    [Fact]
    public void Reject_Approved_ThrowsConflict()
    {
        SellerApplication application = CreatePending();
        application.Approve(AdminId, Now);

        ConflictException exception = Assert.Throws<ConflictException>(() =>
            application.Reject(AdminId, "Too late to reject this application.", Now));

        Assert.Equal(SellerErrorCodes.ApplicationNotPending, exception.Code);
    }

    [Fact]
    public void Cancel_Pending_SetsCancelled()
    {
        SellerApplication application = CreatePending();
        application.Cancel();
        Assert.Equal(SellerApplicationStatus.Cancelled, application.Status);
    }

    [Fact]
    public void Cancel_Rejected_ThrowsConflict()
    {
        SellerApplication application = CreatePending();
        application.Reject(AdminId, "Please provide a more detailed business description.", Now);

        Assert.Throws<ConflictException>(() => application.Cancel());
    }

    private static SellerApplication CreatePending()
    {
        return SellerApplication.Submit(
            UserId,
            "Abdo Handmade",
            "Handmade accessories and crafts studio.",
            "+201000000001");
    }
}
