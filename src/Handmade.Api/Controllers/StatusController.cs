using Handmade.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

/// <summary>
/// Foundation status endpoint used to validate API versioning. No business logic.
/// </summary>
[ApiController]
[Route(ApiRoutes.Status)]
public sealed class StatusController : ControllerBase
{
    /// <summary>
    /// Returns a minimal status payload for the v1 API surface.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "Handmade.Api",
            version = "v1",
            status = "ok"
        });
    }
}
