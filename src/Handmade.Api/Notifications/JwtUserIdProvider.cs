using Handmade.Application.Identity;
using Microsoft.AspNetCore.SignalR;

namespace Handmade.Api.Notifications;

public sealed class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(AuthClaimTypes.Subject)?.Value
               ?? connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
