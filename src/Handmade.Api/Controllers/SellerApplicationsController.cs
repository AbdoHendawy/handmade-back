using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Application.Seller.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.SellerApplications)]
[Authorize]
public sealed class SellerApplicationsController : ControllerBase
{
    private readonly ISellerApplicationService _sellerApplicationService;

    public SellerApplicationsController(ISellerApplicationService sellerApplicationService)
    {
        _sellerApplicationService = sellerApplicationService;
    }

    /// <summary>Submit a seller application for the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SellerApplicationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<SellerApplicationResponse>> Submit(
        [FromBody] SubmitSellerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        SellerApplicationResponse response = await _sellerApplicationService.SubmitAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>List seller applications for the authenticated user.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IReadOnlyList<SellerApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SellerApplicationResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        return Ok(await _sellerApplicationService.GetMineAsync(cancellationToken));
    }

    /// <summary>Cancel the authenticated user's own pending application.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(SellerApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerApplicationResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _sellerApplicationService.CancelAsync(id, cancellationToken));
    }
}
