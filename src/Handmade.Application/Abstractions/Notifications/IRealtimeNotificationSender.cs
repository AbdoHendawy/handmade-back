namespace Handmade.Application.Abstractions.Notifications;

public sealed record UserNotificationMessage(
    Guid NotificationId,
    Guid UserId,
    string Type,
    string Title,
    string Body,
    string? DataJson,
    DateTimeOffset CreatedAt);

public interface IRealtimeNotificationSender
{
    Task SendToUserAsync(UserNotificationMessage message, CancellationToken cancellationToken = default);

    Task SendToRoleAsync(string roleName, UserNotificationMessage message, CancellationToken cancellationToken = default);
}
