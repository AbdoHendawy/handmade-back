namespace Handmade.Application.Notifications;

public static class NotificationLimits
{
    public const int TypeMaxLength = 128;
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 2000;
    public const int DataJsonMaxLength = 4000;
    public const int IdempotencyKeyMaxLength = 256;
    public const int LastErrorMaxLength = 1000;
}

public static class NotificationHubRoutes
{
    public const string Notifications = "/hubs/notifications";
}

public static class NotificationHubMethods
{
    public const string NotificationReceived = "notificationReceived";
}

public static class NotificationGroups
{
    public static string ForUser(Guid userId) => $"user:{userId:D}";

    public static string ForRole(string roleName) => $"role:{roleName}";
}
