using Handmade.Application.Common;
using Handmade.Application.Seller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

/// <summary>
/// Smoke check for the reusable SellerActive policy (role is insufficient; profile must be Active).
/// </summary>
[ApiController]
[Route(ApiRoutes.Seller)]
[Authorize(Policy = AuthorizationPolicies.SellerActive)]
public sealed class SellerAccessController : ControllerBase
{
    [HttpGet("access")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Access()
    {
        return Ok(new { status = "ok", policy = AuthorizationPolicies.SellerActive });
    }
}
