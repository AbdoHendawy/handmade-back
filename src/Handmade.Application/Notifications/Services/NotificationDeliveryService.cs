using Handmade.Application.Abstractions.Email;
using Handmade.Application.Abstractions.Notifications;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Identity.Email;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Seller.Email;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Notifications.Services;

public sealed class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly IApplicationDbContext _db;
    private readonly IRealtimeNotificationSender _realtime;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationDeliveryService> _logger;

    public NotificationDeliveryService(
        IApplicationDbContext db,
        IRealtimeNotificationSender realtime,
        IEmailSender emailSender,
        ILogger<NotificationDeliveryService> logger)
    {
        _db = db;
        _realtime = realtime;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task DeliverAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        Notification? notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification is null)
        {
            _logger.LogWarning("Notification {NotificationId} was not found for delivery.", notificationId);
            return;
        }

        if (!notification.CanDeliver)
        {
            return;
        }

        try
        {
            UserNotificationMessage message = new(
                notification.Id,
                notification.UserId,
                notification.Type,
                notification.Title,
                notification.Body,
                notification.DataJson,
                notification.CreatedAt);

            await _realtime.SendToUserAsync(message, cancellationToken);
            await TrySendEmailAsync(notification, cancellationToken);

            notification.MarkDelivered();
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            notification.RegisterFailedAttempt(ex.Message);
            await _db.SaveChangesAsync(cancellationToken);

            if (notification.DeliveryStatus == NotificationDeliveryStatus.Failed)
            {
                _logger.LogError(
                    ex,
                    "Notification {NotificationId} delivery exhausted retries. In-app record remains.",
                    notificationId);
                return;
            }

            throw;
        }
    }

    private async Task TrySendEmailAsync(Notification notification, CancellationToken cancellationToken)
    {
        User? user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        EmailMessage? email = CreateEmail(notification.Type, user.Email, user.FirstName);
        if (email is null)
        {
            return;
        }

        try
        {
            await _emailSender.SendAsync(email, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Email channel failed for notification {NotificationId}. Real-time delivery still proceeds.",
                notification.Id);
        }
    }

    private static EmailMessage? CreateEmail(string type, string email, string firstName)
    {
        return type switch
        {
            NotificationTypes.Welcome => WelcomeEmailTemplate.Create(email, firstName),
            NotificationTypes.SellerApplicationSubmitted => SellerEmailTemplates.ApplicationSubmitted(email, firstName),
            NotificationTypes.SellerApplicationApproved => SellerEmailTemplates.ApplicationApproved(email, firstName),
            NotificationTypes.SellerApplicationRejected => SellerEmailTemplates.ApplicationRejected(email, firstName),
            NotificationTypes.SellerSuspended => SellerEmailTemplates.SellerSuspended(email, firstName),
            NotificationTypes.SellerReactivated => SellerEmailTemplates.SellerReactivated(email, firstName),
            _ => null
        };
    }
}
