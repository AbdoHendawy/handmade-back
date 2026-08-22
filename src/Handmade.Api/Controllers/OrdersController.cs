using Handmade.Application.Common;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Orders)]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly ICustomerOrderService _orders;

    public OrdersController(ICustomerOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>List the authenticated customer's order groups, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderGroupListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderGroupListItemResponse>>> List(
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _orders.ListMineAsync(paging, cancellationToken));
    }

    /// <summary>Get one of the authenticated customer's order groups with nested seller orders.</summary>
    [HttpGet("{orderGroupId:guid}")]
    [ProducesResponseType(typeof(OrderGroupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderGroupResponse>> GetById(
        Guid orderGroupId,
        CancellationToken cancellationToken)
    {
        return Ok(await _orders.GetByIdAsync(orderGroupId, cancellationToken));
    }
}
