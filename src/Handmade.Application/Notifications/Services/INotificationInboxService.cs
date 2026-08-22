using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;

namespace Handmade.Application.Notifications.Services;

public interface INotificationInboxService
{
    Task<PagedResult<NotificationResponse>> ListMineAsync(
        bool unreadOnly,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> GetMineAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<NotificationResponse> CreateMineAsync(
        CreateInboxNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> UpdateMineAsync(
        Guid notificationId,
        UpdateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteMineAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task DeleteAllMineAsync(CancellationToken cancellationToken = default);

    Task<NotificationResponse> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
