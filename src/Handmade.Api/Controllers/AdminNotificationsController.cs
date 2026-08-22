using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.AdminNotifications)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly IAdminNotificationService _adminNotifications;

    public AdminNotificationsController(IAdminNotificationService adminNotifications)
    {
        _adminNotifications = adminNotifications;
    }

    /// <summary>List all notifications, optionally filtered by user and unread state.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> List(
        [FromQuery] Guid? userId,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] PagingQuery? paging = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _adminNotifications.ListAsync(
            userId,
            unreadOnly,
            paging ?? new PagingQuery(),
            cancellationToken));
    }

    /// <summary>Get any notification by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _adminNotifications.GetAsync(id, cancellationToken));
    }

    /// <summary>Create a notification for a user or fan-out to a role, then enqueue delivery.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AdminCreateNotificationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] AdminCreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            AdminCreateNotificationResponse created =
                await _adminNotifications.CreateForRoleAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }

        NotificationResponse item = await _adminNotifications.CreateForUserAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, item);
    }

    /// <summary>Update any notification's content and read state.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> Update(
        Guid id,
        [FromBody] UpdateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminNotifications.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Delete any notification.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _adminNotifications.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
