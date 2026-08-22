using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller.Events;

namespace Handmade.Domain.Seller;

public sealed class SellerProfile : AggregateRoot, IAuditable
{
    private SellerProfile()
    {
    }

    private SellerProfile(
        Guid id,
        Guid userId,
        Guid sourceApplicationId,
        string businessName,
        string description,
        string phone,
        DateTimeOffset approvedAt)
        : base(id)
    {
        UserId = userId;
        SourceApplicationId = sourceApplicationId;
        BusinessName = businessName;
        Description = description;
        Phone = phone;
        Status = SellerProfileStatus.Active;
        ApprovedAt = approvedAt;
    }

    public Guid UserId { get; private set; }

    public Guid SourceApplicationId { get; private set; }

    public string BusinessName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public SellerProfileStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset ApprovedAt { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public Guid? SuspendedBy { get; private set; }

    public string? SuspensionReason { get; private set; }

    public bool IsActive => Status == SellerProfileStatus.Active;

    public static SellerProfile CreateFromApproval(SellerApplication application, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Status != SellerApplicationStatus.Approved)
        {
            throw new DomainException("A seller profile can only be created from an approved application.")
            {
                Code = SellerErrorCodes.ApplicationNotApproved
            };
        }

        if (application.ReviewedBy is null || application.ReviewedAt is null)
        {
            throw new DomainException("Approved applications must record reviewer details.")
            {
                Code = SellerErrorCodes.ApplicationNotApproved
            };
        }

        SellerProfile profile = new(
            CreateId(),
            application.UserId,
            application.Id,
            application.BusinessName,
            application.Description,
            application.Phone,
            now);

        profile.Raise(new SellerApplicationApproved(
            application.Id,
            profile.Id,
            application.UserId,
            application.ReviewedBy.Value,
            application.ReviewedAt.Value));

        return profile;
    }

    public void UpdateProfile(string businessName, string description, string phone)
    {
        BusinessName = SellerApplication.RequireBusinessName(businessName);
        Description = SellerApplication.RequireDescription(description);
        Phone = SellerApplication.RequirePhone(phone);
    }

    public void Suspend(Guid suspendedBy, string reason, DateTimeOffset now)
    {
        if (Status != SellerProfileStatus.Active)
        {
            throw new ConflictException("Only an active seller can be suspended.")
            {
                Code = SellerErrorCodes.ProfileNotActive
            };
        }

        if (suspendedBy == Guid.Empty)
        {
            throw new DomainException("Reviewer is required.") { Code = "invalid_reviewer" };
        }

        string trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException("Suspension reason is required.")
            {
                Code = SellerErrorCodes.SuspensionReasonRequired
            };
        }

        Status = SellerProfileStatus.Suspended;
        SuspendedAt = now;
        SuspendedBy = suspendedBy;
        SuspensionReason = trimmedReason;
        Raise(new SellerSuspended(Id, UserId, suspendedBy, now));
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status != SellerProfileStatus.Suspended)
        {
            throw new ConflictException("Only a suspended seller can be reactivated.")
            {
                Code = SellerErrorCodes.ProfileNotSuspended
            };
        }

        Status = SellerProfileStatus.Active;
        Raise(new SellerReactivated(Id, UserId, now));
    }
}
