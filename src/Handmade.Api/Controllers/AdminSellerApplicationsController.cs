using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Application.Seller.Services;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.AdminSellerApplications)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminSellerApplicationsController : ControllerBase
{
    private readonly IAdminSellerService _adminSellerService;

    public AdminSellerApplicationsController(IAdminSellerService adminSellerService)
    {
        _adminSellerService = adminSellerService;
    }

    /// <summary>List seller applications with optional status filter and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SellerApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SellerApplicationResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _adminSellerService.ListApplicationsAsync(status, paging, cancellationToken));
    }

    /// <summary>Get a seller application by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SellerApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerApplicationResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.GetApplicationAsync(id, cancellationToken));
    }

    /// <summary>Approve a pending seller application.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(SellerApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerApplicationResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.ApproveAsync(id, cancellationToken));
    }

    /// <summary>Reject a pending seller application.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(SellerApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerApplicationResponse>> Reject(
        Guid id,
        [FromBody] RejectSellerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminSellerService.RejectAsync(id, request, cancellationToken));
    }
}
