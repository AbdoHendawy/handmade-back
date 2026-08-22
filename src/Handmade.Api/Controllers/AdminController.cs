using Handmade.Application.Common;
using Handmade.Application.Identity.Services;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Admin)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AdminController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>Authorization smoke check for Admin role.</summary>
    [HttpGet("ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        return Ok(new { status = "ok", role = RoleNames.Admin });
    }

    /// <summary>Force-logout a user from all sessions immediately.</summary>
    [HttpPost("users/{userId:guid}/revoke-sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken)
    {
        string? ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authenticationService.RevokeAllSessionsAsync(userId, ip, cancellationToken);
        return NoContent();
    }
}
