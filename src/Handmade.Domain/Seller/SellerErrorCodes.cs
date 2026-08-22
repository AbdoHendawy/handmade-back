namespace Handmade.Domain.Seller;

/// <summary>
/// Stable seller error codes returned via ProblemDetails.
/// </summary>
public static class SellerErrorCodes
{
    public const string InvalidBusinessName = "invalid_business_name";
    public const string InvalidDescription = "invalid_description";
    public const string InvalidPhone = "invalid_phone";
    public const string ApplicationNotPending = "application_not_pending";
    public const string ApplicationNotFound = "application_not_found";
    public const string CannotApproveOwnApplication = "cannot_approve_own_application";
    public const string PendingApplicationExists = "pending_application_exists";
    public const string AlreadySeller = "already_seller";
    public const string ProfileNotFound = "profile_not_found";
    public const string ProfileNotActive = "profile_not_active";
    public const string ProfileNotSuspended = "profile_not_suspended";
    public const string ApplicationNotApproved = "application_not_approved";
    public const string RejectionReasonRequired = "rejection_reason_required";
    public const string SuspensionReasonRequired = "suspension_reason_required";
    public const string ConcurrencyConflict = "concurrency_conflict";
}
