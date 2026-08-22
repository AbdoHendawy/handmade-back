using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Application.Seller.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.SellerProfile)]
[Authorize]
public sealed class SellerProfileController : ControllerBase
{
    private readonly ISellerProfileService _sellerProfileService;

    public SellerProfileController(ISellerProfileService sellerProfileService)
    {
        _sellerProfileService = sellerProfileService;
    }

    /// <summary>Get the authenticated user's seller profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SellerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _sellerProfileService.GetMineAsync(cancellationToken));
    }

    /// <summary>Update allowed seller profile fields for the authenticated user.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(SellerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> UpdateMine(
        [FromBody] UpdateSellerProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _sellerProfileService.UpdateMineAsync(request, cancellationToken));
    }
}
