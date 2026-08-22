using Handmade.Application.Abstractions.Notifications;
using Handmade.Application.Notifications;

namespace Handmade.Infrastructure.Notifications;

public sealed class NoOpRealtimeNotificationSender : IRealtimeNotificationSender
{
    public Task SendToUserAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendToRoleAsync(string roleName, UserNotificationMessage message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
