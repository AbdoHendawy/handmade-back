using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Common;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.AdminCategories)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminCategoriesController : ControllerBase
{
    private readonly IAdminCategoryService _categories;

    public AdminCategoriesController(IAdminCategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTreeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryTreeResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _categories.ListTreeAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _categories.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        CategoryResponse created = await _categories.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _categories.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _categories.ActivateAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _categories.DeactivateAsync(id, cancellationToken));
    }
}

[ApiController]
[Route(ApiRoutes.AdminProducts)]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminProductsController : ControllerBase
{
    private readonly IAdminProductService _products;

    public AdminProductsController(IAdminProductService products)
    {
        _products = products;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? sellerId,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _products.ListAsync(status, sellerId, categoryId, paging, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.GetAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Approve(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.ApproveAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Reject(
        Guid id,
        [FromBody] RejectProductRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _products.RejectAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _products.ArchiveAsync(id, cancellationToken));
    }
}
