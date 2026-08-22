using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Common;
using Handmade.Application.Seller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.SellerProducts)]
[Authorize(Policy = AuthorizationPolicies.SellerActive)]
public sealed class SellerProductsController : ControllerBase
{
    private readonly ISellerProductService _products;

    public SellerProductsController(ISellerProductService products)
    {
        _products = products;
    }

    /// <summary>List the current active seller's products.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _products.ListMineAsync(status, paging, cancellationToken));
    }

    /// <summary>Get one owned product including private status and rejection reason.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.GetMineAsync(id, cancellationToken));
    }

    /// <summary>Create a draft product. SellerId and status are server-controlled.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        ProductResponse created = await _products.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Update an owned draft, rejected, or published product.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Delete a draft or rejected product. Published products must be archived.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _products.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Submit a draft or rejected product for admin review.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Submit(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.SubmitAsync(id, cancellationToken));
    }

    /// <summary>Return a pending product to draft so it can be edited.</summary>
    [HttpPost("{id:guid}/cancel-submit")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> CancelSubmit(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.CancelSubmitAsync(id, cancellationToken));
    }

    /// <summary>Archive a published product (removes it from the public catalog).</summary>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.ArchiveAsync(id, cancellationToken));
    }

    /// <summary>Restore an archived product to draft.</summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Restore(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.RestoreAsync(id, cancellationToken));
    }

    /// <summary>Add image metadata. Binary upload uses IFileStorage (not configured in this sprint).</summary>
    [HttpPost("{id:guid}/images")]
    [ProducesResponseType(typeof(ProductImageResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductImageResponse>> AddImage(
        Guid id,
        [FromBody] AddProductImageRequest request,
        CancellationToken cancellationToken)
    {
        ProductImageResponse created = await _products.AddImageAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await _products.DeleteImageAsync(id, imageId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/images/{imageId:guid}/primary")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> SetPrimary(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        return Ok(await _products.SetPrimaryImageAsync(id, imageId, cancellationToken));
    }

    [HttpPut("{id:guid}/images/reorder")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Reorder(
        Guid id,
        [FromBody] ReorderProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.ReorderImagesAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/variants")]
    [ProducesResponseType(typeof(ProductVariantResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductVariantResponse>> AddVariant(
        Guid id,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        ProductVariantResponse created = await _products.AddVariantAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    [ProducesResponseType(typeof(ProductVariantResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductVariantResponse>> UpdateVariant(
        Guid id,
        Guid variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.UpdateVariantAsync(id, variantId, request, cancellationToken));
    }

    [HttpDelete("{id:guid}/variants/{variantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteVariant(Guid id, Guid variantId, CancellationToken cancellationToken)
    {
        await _products.DeleteVariantAsync(id, variantId, cancellationToken);
        return NoContent();
    }

    /// <summary>Set stock for a product that has no variants. Published listings may be restocked.</summary>
    [HttpPut("{productId:guid}/stock")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> SetStock(
        Guid productId,
        [FromBody] SetStockRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.SetStockAsync(productId, request, cancellationToken));
    }

    /// <summary>Set stock for a variant owned by the current seller.</summary>
    [HttpPut("{productId:guid}/variants/{variantId:guid}/stock")]
    [ProducesResponseType(typeof(ProductVariantResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductVariantResponse>> SetVariantStock(
        Guid productId,
        Guid variantId,
        [FromBody] SetStockRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.SetVariantStockAsync(productId, variantId, request, cancellationToken));
    }
}
