using Handmade.Application.Notifications;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Seller.Services;

public interface ISellerNotificationService
{
    Task NotifyApplicationSubmittedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default);

    Task NotifyApplicationApprovedAsync(
        Guid userId,
        Guid applicationId,
        Guid sellerId,
        CancellationToken cancellationToken = default);

    Task NotifyApplicationRejectedAsync(
        Guid userId,
        Guid applicationId,
        string reason,
        CancellationToken cancellationToken = default);

    Task NotifySellerSuspendedAsync(
        Guid userId,
        Guid sellerId,
        string reason,
        CancellationToken cancellationToken = default);

    Task NotifySellerReactivatedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default);
}

public sealed class SellerNotificationService : ISellerNotificationService
{
    private readonly INotificationPublisher _publisher;
    private readonly ILogger<SellerNotificationService> _logger;

    public SellerNotificationService(INotificationPublisher publisher, ILogger<SellerNotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task NotifyApplicationSubmittedAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return TryPublishAsync(
            userId,
            NotificationTypes.SellerApplicationSubmitted,
            "Your Seller Application Was Received",
            "We received your seller application. Our team will review it and contact you with an update.",
            $"{NotificationTypes.SellerApplicationSubmitted}:{applicationId:D}",
            NotificationDataJson.Serialize(new { applicationId }),
            cancellationToken);
    }

    public Task NotifyApplicationApprovedAsync(
        Guid userId,
        Guid applicationId,
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        return TryPublishAsync(
            userId,
            NotificationTypes.SellerApplicationApproved,
            "Congratulations! Your Seller Account Is Approved",
            "Your seller application has been approved. You can now manage your seller profile.",
            $"{NotificationTypes.SellerApplicationApproved}:{applicationId:D}",
            NotificationDataJson.Serialize(new { applicationId, sellerId }),
            cancellationToken);
    }

    public Task NotifyApplicationRejectedAsync(
        Guid userId,
        Guid applicationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        string trimmedReason = reason.Trim();
        return TryPublishAsync(
            userId,
            NotificationTypes.SellerApplicationRejected,
            "Update About Your Seller Application",
            $"Your seller application was not approved. {trimmedReason} You can submit a new application.",
            $"{NotificationTypes.SellerApplicationRejected}:{applicationId:D}",
            NotificationDataJson.Serialize(new { applicationId, reason = trimmedReason }),
            cancellationToken);
    }

    public Task NotifySellerSuspendedAsync(
        Guid userId,
        Guid sellerId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        string trimmedReason = reason.Trim();
        return TryPublishAsync(
            userId,
            NotificationTypes.SellerSuspended,
            "Your Seller Account Has Been Suspended",
            $"Your seller account has been suspended. {trimmedReason}",
            $"{NotificationTypes.SellerSuspended}:{sellerId:D}",
            NotificationDataJson.Serialize(new { sellerId, reason = trimmedReason }),
            cancellationToken);
    }

    public Task NotifySellerReactivatedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default)
    {
        return TryPublishAsync(
            userId,
            NotificationTypes.SellerReactivated,
            "Your Seller Account Has Been Reactivated",
            "Your seller account is active again. You can resume seller activity.",
            $"{NotificationTypes.SellerReactivated}:{sellerId:D}",
            NotificationDataJson.Serialize(new { sellerId }),
            cancellationToken);
    }

    private async Task TryPublishAsync(
        Guid userId,
        string type,
        string title,
        string body,
        string idempotencyKey,
        string dataJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishToUserAsync(
                new CreateUserNotificationRequest(userId, type, title, body, idempotencyKey, dataJson),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist {NotificationType} for user {UserId}. Business state is unchanged.",
                type,
                userId);
        }
    }
}
