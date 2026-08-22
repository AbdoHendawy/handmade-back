using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;

namespace Handmade.Application.Notifications.Services;

public interface IAdminNotificationService
{
    Task<PagedResult<NotificationResponse>> ListAsync(
        Guid? userId,
        bool unreadOnly,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> GetAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<NotificationResponse> CreateForUserAsync(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminCreateNotificationResponse> CreateForRoleAsync(
        AdminCreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> UpdateAsync(
        Guid notificationId,
        UpdateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
