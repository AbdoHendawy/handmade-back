namespace Handmade.Application.Notifications.DTOs;

public sealed record CreateUserNotificationRequest(
    Guid UserId,
    string Type,
    string Title,
    string Body,
    string IdempotencyKey,
    string? DataJson = null);

public sealed record UpdateNotificationRequest(
    string Title,
    string Body,
    bool IsRead,
    string? DataJson = null);

public sealed record AdminCreateNotificationRequest(
    string Type,
    string Title,
    string Body,
    Guid? UserId = null,
    string? RoleName = null,
    string? DataJson = null,
    string? IdempotencyKey = null);

public sealed record AdminCreateNotificationResponse(int CreatedCount);

public sealed record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Body,
    string? DataJson,
    bool IsRead,
    DateTimeOffset? ReadAt,
    string DeliveryStatus,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountResponse(int Count);

public sealed record NotificationDeliveryPayload(
    Guid Id,
    string Type,
    string Title,
    string Message,
    object? Data,
    DateTimeOffset CreatedAt);
