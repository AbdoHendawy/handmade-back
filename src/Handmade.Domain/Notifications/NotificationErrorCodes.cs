namespace Handmade.Domain.Notifications;

public static class NotificationErrorCodes
{
    public const string InvalidUser = "invalid_notification_user";
    public const string InvalidType = "invalid_notification_type";
    public const string InvalidTitle = "invalid_notification_title";
    public const string InvalidIdempotencyKey = "invalid_idempotency_key";
    public const string AlreadyRead = "notification_already_read";
}
