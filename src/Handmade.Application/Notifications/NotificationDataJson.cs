using System.Text.Json;

namespace Handmade.Application.Notifications;

internal static class NotificationDataJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
