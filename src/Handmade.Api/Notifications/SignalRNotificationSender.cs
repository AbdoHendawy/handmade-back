using Handmade.Application.Abstractions.Notifications;
using Handmade.Application.Notifications;
using Handmade.Application.Notifications.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Handmade.Api.Notifications;

public sealed class SignalRNotificationSender : IRealtimeNotificationSender
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationSender(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task SendToUserAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(NotificationGroups.ForUser(message.UserId))
            .SendAsync(NotificationHubMethods.NotificationReceived, ToPayload(message), cancellationToken);
    }

    public Task SendToRoleAsync(string roleName, UserNotificationMessage message, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(NotificationGroups.ForRole(roleName))
            .SendAsync(NotificationHubMethods.NotificationReceived, ToPayload(message), cancellationToken);
    }

    private static NotificationResponse ToPayload(UserNotificationMessage message)
    {
        return new NotificationResponse(
            message.NotificationId,
            message.UserId,
            message.Type,
            message.Title,
            message.Body,
            message.DataJson,
            IsRead: false,
            ReadAt: null,
            DeliveryStatus: "Pending",
            message.CreatedAt);
    }
}
