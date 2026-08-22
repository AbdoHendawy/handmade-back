using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Notifications;

public sealed class Notification : AggregateRoot, IAuditable
{
    public const int MaxDeliveryAttempts = 5;

    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid userId,
        string type,
        string title,
        string body,
        string? dataJson,
        string idempotencyKey)
        : base(id)
    {
        UserId = userId;
        Type = type;
        Title = title;
        Body = body;
        DataJson = dataJson;
        IdempotencyKey = idempotencyKey;
        IsRead = false;
        DeliveryStatus = NotificationDeliveryStatus.Pending;
        AttemptCount = 0;
    }

    public Guid UserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? DataJson { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public NotificationDeliveryStatus DeliveryStatus { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Notification CreateForUser(
        Guid userId,
        string type,
        string title,
        string body,
        string idempotencyKey,
        string? dataJson = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Notification user is required.") { Code = NotificationErrorCodes.InvalidUser };
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainException("Notification type is required.") { Code = NotificationErrorCodes.InvalidType };
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Notification title is required.") { Code = NotificationErrorCodes.InvalidTitle };
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency key is required.")
            {
                Code = NotificationErrorCodes.InvalidIdempotencyKey
            };
        }

        return new Notification(
            CreateId(),
            userId,
            type.Trim(),
            title.Trim(),
            (body ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(dataJson) ? null : dataJson.Trim(),
            idempotencyKey.Trim());
    }

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = now;
    }

    public void MarkUnread()
    {
        IsRead = false;
        ReadAt = null;
    }

    public void UpdateContent(string title, string body, string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Notification title is required.") { Code = NotificationErrorCodes.InvalidTitle };
        }

        Title = title.Trim();
        Body = (body ?? string.Empty).Trim();
        DataJson = string.IsNullOrWhiteSpace(dataJson) ? null : dataJson.Trim();
    }

    public void MarkDelivered()
    {
        DeliveryStatus = NotificationDeliveryStatus.Delivered;
        LastError = null;
    }

    public void RegisterFailedAttempt(string error)
    {
        AttemptCount++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Delivery failed." : error.Trim();

        if (AttemptCount >= MaxDeliveryAttempts)
        {
            DeliveryStatus = NotificationDeliveryStatus.Failed;
        }
    }

    public bool CanDeliver => DeliveryStatus == NotificationDeliveryStatus.Pending;
}
