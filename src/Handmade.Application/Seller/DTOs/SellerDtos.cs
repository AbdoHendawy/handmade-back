namespace Handmade.Application.Seller.DTOs;

public sealed record SubmitSellerApplicationRequest(
    string BusinessName,
    string Description,
    string Phone);

public sealed record RejectSellerApplicationRequest(string Reason);

public sealed record SuspendSellerRequest(string Reason);

public sealed record UpdateSellerProfileRequest(
    string BusinessName,
    string Description,
    string Phone);

public sealed record SellerApplicationResponse(
    Guid Id,
    Guid UserId,
    string Status,
    string BusinessName,
    string Description,
    string Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy,
    string? RejectionReason);

public sealed record SellerProfileResponse(
    Guid Id,
    Guid UserId,
    Guid SourceApplicationId,
    string Status,
    string BusinessName,
    string Description,
    string Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SuspendedAt,
    Guid? SuspendedBy,
    string? SuspensionReason);
