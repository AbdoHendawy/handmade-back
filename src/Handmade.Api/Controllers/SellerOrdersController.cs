using Handmade.Application.Common;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Orders.Services;
using Handmade.Application.Seller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.SellerOrders)]
[Authorize(Policy = AuthorizationPolicies.SellerActive)]
public sealed class SellerOrdersController : ControllerBase
{
    private readonly ISellerOrderService _orders;

    public SellerOrdersController(ISellerOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>List orders placed with the current active seller, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _orders.ListMineAsync(paging, cancellationToken));
    }

    /// <summary>Get one owned order. Unknown or other-seller ids return 404.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Get(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.GetMineAsync(orderId, cancellationToken));
    }

    /// <summary>Confirm an owned placed order.</summary>
    [HttpPost("{orderId:guid}/confirm")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.ConfirmAsync(orderId, cancellationToken));
    }

    /// <summary>Mark an owned confirmed order as preparing.</summary>
    [HttpPost("{orderId:guid}/prepare")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Prepare(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.PrepareAsync(orderId, cancellationToken));
    }

    /// <summary>Ship an owned preparing order.</summary>
    [HttpPost("{orderId:guid}/ship")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Ship(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.ShipAsync(orderId, cancellationToken));
    }

    /// <summary>Deliver an owned shipped order.</summary>
    [HttpPost("{orderId:guid}/deliver")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Deliver(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.DeliverAsync(orderId, cancellationToken));
    }

    /// <summary>Cancel an owned placed order.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid orderId, CancellationToken cancellationToken)
    {
        return Ok(await _orders.CancelAsync(orderId, cancellationToken));
    }
}
