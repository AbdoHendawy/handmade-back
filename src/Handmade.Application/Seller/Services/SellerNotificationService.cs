using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Notifications;

namespace Handmade.Application.Seller.Services;

public interface ISellerNotificationService
{
    Task NotifyApplicationSubmittedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default);

    Task NotifyApplicationApprovedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default);

    Task NotifyApplicationRejectedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default);

    Task NotifySellerSuspendedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default);

    Task NotifySellerReactivatedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default);
}

public sealed class SellerNotificationService : ISellerNotificationService
{
    private readonly INotificationPublisher _publisher;

    public SellerNotificationService(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task NotifyApplicationSubmittedAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return Publish(
            userId,
            NotificationTypes.SellerApplicationSubmitted,
            "Your Seller Application Was Received",
            "We received your seller application. Our team will review it and contact you with an update.",
            $"{NotificationTypes.SellerApplicationSubmitted}:{applicationId:D}",
            cancellationToken);
    }

    public Task NotifyApplicationApprovedAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return Publish(
            userId,
            NotificationTypes.SellerApplicationApproved,
            "Congratulations! Your Seller Account Is Approved",
            "Your seller application has been approved. You can now manage your seller profile.",
            $"{NotificationTypes.SellerApplicationApproved}:{applicationId:D}",
            cancellationToken);
    }

    public Task NotifyApplicationRejectedAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return Publish(
            userId,
            NotificationTypes.SellerApplicationRejected,
            "Update About Your Seller Application",
            "Your seller application was not approved. You can review the reason in the app and submit a new application.",
            $"{NotificationTypes.SellerApplicationRejected}:{applicationId:D}",
            cancellationToken);
    }

    public Task NotifySellerSuspendedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default)
    {
        return Publish(
            userId,
            NotificationTypes.SellerSuspended,
            "Your Seller Account Has Been Suspended",
            "Your seller account has been suspended and seller-only actions are currently unavailable.",
            $"{NotificationTypes.SellerSuspended}:{sellerId:D}",
            cancellationToken);
    }

    public Task NotifySellerReactivatedAsync(Guid userId, Guid sellerId, CancellationToken cancellationToken = default)
    {
        return Publish(
            userId,
            NotificationTypes.SellerReactivated,
            "Your Seller Account Has Been Reactivated",
            "Your seller account is active again. You can resume seller activity.",
            $"{NotificationTypes.SellerReactivated}:{sellerId:D}",
            cancellationToken);
    }

    private Task Publish(
        Guid userId,
        string type,
        string title,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _publisher.PublishToUserAsync(
            new CreateUserNotificationRequest(userId, type, title, body, idempotencyKey),
            cancellationToken);
    }
}
