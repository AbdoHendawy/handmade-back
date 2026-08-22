using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Application.Seller.Services;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.AdminSellers)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminSellersController : ControllerBase
{
    private readonly IAdminSellerService _adminSellerService;

    public AdminSellersController(IAdminSellerService adminSellerService)
    {
        _adminSellerService = adminSellerService;
    }

    /// <summary>List seller profiles with optional status filter and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SellerProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SellerProfileResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _adminSellerService.ListSellersAsync(status, paging, cancellationToken));
    }

    /// <summary>Get a seller profile by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SellerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.GetSellerAsync(id, cancellationToken));
    }

    /// <summary>Suspend an active seller.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(SellerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> Suspend(
        Guid id,
        [FromBody] SuspendSellerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.SuspendAsync(id, request, cancellationToken));
    }

    /// <summary>Reactivate a suspended seller.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(SellerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileResponse>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.ReactivateAsync(id, cancellationToken));
    }
}
