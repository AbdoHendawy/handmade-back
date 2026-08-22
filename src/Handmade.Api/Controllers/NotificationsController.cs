using Handmade.Application.Common;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Notifications)]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationInboxService _inbox;

    public NotificationsController(INotificationInboxService inbox)
    {
        _inbox = inbox;
    }

    /// <summary>List the authenticated user's persistent notifications (newest first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] PagingQuery? paging = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _inbox.ListMineAsync(unreadOnly, paging ?? new PagingQuery(), cancellationToken));
    }

    /// <summary>Unread count for the authenticated user.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await _inbox.GetUnreadCountAsync(cancellationToken));
    }

    /// <summary>Get one of the authenticated user's notifications.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _inbox.GetMineAsync(id, cancellationToken));
    }

    /// <summary>Create a notification for the authenticated user and enqueue delivery.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NotificationResponse>> Create(
        [FromBody] CreateInboxNotificationRequest request,
        CancellationToken cancellationToken)
    {
        NotificationResponse created = await _inbox.CreateMineAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Update title, body, data, and read state for an owned notification.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> Update(
        Guid id,
        [FromBody] UpdateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _inbox.UpdateMineAsync(id, request, cancellationToken));
    }

    /// <summary>Delete one owned notification.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _inbox.DeleteMineAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Delete every notification for the authenticated user.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        await _inbox.DeleteAllMineAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Mark a single notification as read. Idempotent if already read.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _inbox.MarkReadAsync(id, cancellationToken));
    }

    /// <summary>Mark every unread notification as read for the authenticated user.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _inbox.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
