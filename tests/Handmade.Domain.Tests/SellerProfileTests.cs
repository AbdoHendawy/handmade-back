using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Handmade.Domain.Seller.Events;

namespace Handmade.Domain.Tests;

public sealed class SellerProfileTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateFromApproval_ApprovedApplication_CreatesActiveProfileAndRaisesEvent()
    {
        SellerApplication application = CreateApproved();

        SellerProfile profile = SellerProfile.CreateFromApproval(application, Now);

        Assert.Equal(SellerProfileStatus.Active, profile.Status);
        Assert.Equal(UserId, profile.UserId);
        Assert.Equal(application.Id, profile.SourceApplicationId);
        Assert.Equal(application.BusinessName, profile.BusinessName);
        Assert.Equal(Now, profile.ApprovedAt);
        Assert.Contains(profile.DomainEvents, e => e is SellerApplicationApproved approved
            && approved.SellerId == profile.Id
            && approved.ApplicationId == application.Id
            && approved.ApprovedBy == AdminId);
    }

    [Fact]
    public void CreateFromApproval_PendingApplication_Throws()
    {
        SellerApplication application = SellerApplication.Submit(
            UserId,
            "Abdo Handmade",
            "Handmade accessories and crafts studio.",
            "+201000000001");

        DomainException exception = Assert.Throws<DomainException>(() =>
            SellerProfile.CreateFromApproval(application, Now));

        Assert.Equal(SellerErrorCodes.ApplicationNotApproved, exception.Code);
    }

    [Fact]
    public void UpdateProfile_ChangesAllowedFields_DoesNotChangeStatusOrUserId()
    {
        SellerProfile profile = CreateActive();
        Guid originalUserId = profile.UserId;
        SellerProfileStatus originalStatus = profile.Status;

        profile.UpdateProfile("New Name", "Updated description for the handmade studio.", "+201000000099");

        Assert.Equal("New Name", profile.BusinessName);
        Assert.Equal("Updated description for the handmade studio.", profile.Description);
        Assert.Equal("+201000000099", profile.Phone);
        Assert.Equal(originalUserId, profile.UserId);
        Assert.Equal(originalStatus, profile.Status);
    }

    [Fact]
    public void Suspend_Active_SetsSuspendedAndRaisesEvent()
    {
        SellerProfile profile = CreateActive();

        profile.Suspend(AdminId, "Policy violation", Now);

        Assert.Equal(SellerProfileStatus.Suspended, profile.Status);
        Assert.Equal(AdminId, profile.SuspendedBy);
        Assert.Equal("Policy violation", profile.SuspensionReason);
        Assert.Equal(Now, profile.SuspendedAt);
        Assert.Contains(profile.DomainEvents, e => e is SellerSuspended);
    }

    [Fact]
    public void Suspend_AlreadySuspended_ThrowsConflict()
    {
        SellerProfile profile = CreateActive();
        profile.Suspend(AdminId, "Policy violation", Now);

        ConflictException exception = Assert.Throws<ConflictException>(() =>
            profile.Suspend(AdminId, "Again", Now));

        Assert.Equal(SellerErrorCodes.ProfileNotActive, exception.Code);
    }

    [Fact]
    public void Suspend_EmptyReason_Throws()
    {
        SellerProfile profile = CreateActive();

        DomainException exception = Assert.Throws<DomainException>(() =>
            profile.Suspend(AdminId, " ", Now));

        Assert.Equal(SellerErrorCodes.SuspensionReasonRequired, exception.Code);
    }

    [Fact]
    public void Reactivate_Suspended_SetsActiveAndRaisesEvent()
    {
        SellerProfile profile = CreateActive();
        profile.Suspend(AdminId, "Policy violation", Now);

        profile.Reactivate(Now);

        Assert.Equal(SellerProfileStatus.Active, profile.Status);
        Assert.Contains(profile.DomainEvents, e => e is SellerReactivated);
    }

    [Fact]
    public void Reactivate_Active_ThrowsConflict()
    {
        SellerProfile profile = CreateActive();

        ConflictException exception = Assert.Throws<ConflictException>(() =>
            profile.Reactivate(Now));

        Assert.Equal(SellerErrorCodes.ProfileNotSuspended, exception.Code);
    }

    private static SellerApplication CreateApproved()
    {
        SellerApplication application = SellerApplication.Submit(
            UserId,
            "Abdo Handmade",
            "Handmade accessories and crafts studio.",
            "+201000000001");
        application.Approve(AdminId, Now);
        return application;
    }

    private static SellerProfile CreateActive()
    {
        return SellerProfile.CreateFromApproval(CreateApproved(), Now);
    }
}
