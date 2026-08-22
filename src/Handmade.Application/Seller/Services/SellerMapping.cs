using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Seller.DTOs;

namespace Handmade.Application.Seller.Services;

internal static class SellerMapping
{
    public static Guid RequireUserId(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return currentUser.UserId.Value;
    }

    public static SellerApplicationResponse ToResponse(Domain.Seller.SellerApplication application)
    {
        return new SellerApplicationResponse(
            application.Id,
            application.UserId,
            application.Status.ToString(),
            application.BusinessName,
            application.Description,
            application.Phone,
            application.CreatedAt,
            application.UpdatedAt,
            application.ReviewedAt,
            application.ReviewedBy,
            application.RejectionReason);
    }

    public static SellerProfileResponse ToResponse(Domain.Seller.SellerProfile profile)
    {
        return new SellerProfileResponse(
            profile.Id,
            profile.UserId,
            profile.SourceApplicationId,
            profile.Status.ToString(),
            profile.BusinessName,
            profile.Description,
            profile.Phone,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.ApprovedAt,
            profile.SuspendedAt,
            profile.SuspendedBy,
            profile.SuspensionReason);
    }
}
