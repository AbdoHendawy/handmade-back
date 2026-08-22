using Handmade.Application.Notifications;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Identity.Services;

public interface IIdentityNotificationService
{
    Task NotifyWelcomeAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class IdentityNotificationService : IIdentityNotificationService
{
    private readonly INotificationPublisher _publisher;
    private readonly ILogger<IdentityNotificationService> _logger;

    public IdentityNotificationService(
        INotificationPublisher publisher,
        ILogger<IdentityNotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task NotifyWelcomeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _publisher.PublishToUserAsync(
                new CreateUserNotificationRequest(
                    userId,
                    NotificationTypes.Welcome,
                    "Welcome to Handmade",
                    "Your account has been successfully created. You can now start exploring Handmade.",
                    $"{NotificationTypes.Welcome}:{userId:D}",
                    NotificationDataJson.Serialize(new { userId })),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Welcome notification failed for user {UserId}. Account remains active.",
                userId);
        }
    }
}
