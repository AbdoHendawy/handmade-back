using Handmade.Application.Cart.DTOs;
using Handmade.Application.Cart.Services;
using Handmade.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Cart)]
[Authorize]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _carts;

    public CartController(ICartService carts)
    {
        _carts = carts;
    }

    /// <summary>Get the authenticated user's cart. Returns an empty cart when none exists yet.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _carts.GetMineAsync(cancellationToken));
    }

    /// <summary>Add a product to the cart, or increase quantity if the line already exists.</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartResponse>> Add(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _carts.AddItemAsync(request, cancellationToken));
    }

    /// <summary>Set the quantity of a cart line identified by product and optional variant.</summary>
    [HttpPut("items/{productId:guid}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartResponse>> Update(
        Guid productId,
        [FromBody] UpdateCartItemRequest request,
        [FromQuery] Guid? variantId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _carts.UpdateItemAsync(productId, variantId, request, cancellationToken));
    }

    /// <summary>Remove a cart line identified by product and optional variant.</summary>
    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        Guid productId,
        [FromQuery] Guid? variantId = null,
        CancellationToken cancellationToken = default)
    {
        await _carts.RemoveItemAsync(productId, variantId, cancellationToken);
        return NoContent();
    }

    /// <summary>Remove every item from the authenticated user's cart. Idempotent if the cart does not exist.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _carts.ClearAsync(cancellationToken);
        return NoContent();
    }
}
