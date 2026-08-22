using Handmade.Application.Common;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Checkout)]
[Authorize]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkout;

    public CheckoutController(ICheckoutService checkout)
    {
        _checkout = checkout;
    }

    /// <summary>Place the authenticated user's cart as one order group (one order per seller).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderGroupResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderGroupResponse>> Place(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        OrderGroupResponse created = await _checkout.PlaceAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(OrdersController.GetById),
            "Orders",
            new { orderGroupId = created.Id },
            created);
    }
}
