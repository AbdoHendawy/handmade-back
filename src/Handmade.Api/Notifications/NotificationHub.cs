using System.Security.Claims;
using Handmade.Application.Identity;
using Handmade.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Handmade.Api.Notifications;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out Guid userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.ForUser(userId));
        }

        foreach (Claim role in Context.User?.FindAll(ClaimTypes.Role) ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role.Value))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.ForRole(role.Value));
            }
        }

        await base.OnConnectedAsync();
    }
}
