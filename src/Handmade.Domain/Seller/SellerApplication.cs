using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller.Events;

namespace Handmade.Domain.Seller;

public sealed class SellerApplication : AggregateRoot, IAuditable
{
    private SellerApplication()
    {
    }

    private SellerApplication(
        Guid id,
        Guid userId,
        string businessName,
        string description,
        string phone)
        : base(id)
    {
        UserId = userId;
        BusinessName = businessName;
        Description = description;
        Phone = phone;
        Status = SellerApplicationStatus.Pending;
    }

    public Guid UserId { get; private set; }

    public SellerApplicationStatus Status { get; private set; }

    public string BusinessName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public string? RejectionReason { get; private set; }

    public static SellerApplication Submit(Guid userId, string businessName, string description, string phone)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User is required.") { Code = "invalid_user" };
        }

        SellerApplication application = new(
            CreateId(),
            userId,
            RequireBusinessName(businessName),
            RequireDescription(description),
            RequirePhone(phone));

        application.Raise(new SellerApplicationSubmitted(application.Id, application.UserId));
        return application;
    }

    public void Approve(Guid reviewedBy, DateTimeOffset now)
    {
        EnsurePending();

        if (reviewedBy == Guid.Empty)
        {
            throw new DomainException("Reviewer is required.") { Code = "invalid_reviewer" };
        }

        if (reviewedBy == UserId)
        {
            throw new ForbiddenException("A user cannot approve their own seller application.")
            {
                Code = SellerErrorCodes.CannotApproveOwnApplication
            };
        }

        Status = SellerApplicationStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = now;
        RejectionReason = null;
    }

    public void Reject(Guid reviewedBy, string reason, DateTimeOffset now)
    {
        EnsurePending();

        if (reviewedBy == Guid.Empty)
        {
            throw new DomainException("Reviewer is required.") { Code = "invalid_reviewer" };
        }

        if (reviewedBy == UserId)
        {
            throw new ForbiddenException("A user cannot reject their own seller application.")
            {
                Code = SellerErrorCodes.CannotApproveOwnApplication
            };
        }

        string trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException("Rejection reason is required.")
            {
                Code = SellerErrorCodes.RejectionReasonRequired
            };
        }

        Status = SellerApplicationStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = now;
        RejectionReason = trimmedReason;
        Raise(new SellerApplicationRejected(Id, UserId, reviewedBy, now));
    }

    public void Cancel()
    {
        EnsurePending();
        Status = SellerApplicationStatus.Cancelled;
    }

    private void EnsurePending()
    {
        if (Status != SellerApplicationStatus.Pending)
        {
            throw new ConflictException($"Only pending applications can be reviewed. Current status is {Status}.")
            {
                Code = SellerErrorCodes.ApplicationNotPending
            };
        }
    }

    internal static string RequireBusinessName(string businessName)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainException("Business name is required.") { Code = SellerErrorCodes.InvalidBusinessName };
        }

        return businessName.Trim();
    }

    internal static string RequireDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Description is required.") { Code = SellerErrorCodes.InvalidDescription };
        }

        return description.Trim();
    }

    internal static string RequirePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainException("Phone is required.") { Code = SellerErrorCodes.InvalidPhone };
        }

        return phone.Trim();
    }
}
