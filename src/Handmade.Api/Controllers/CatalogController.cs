using Handmade.Api.Extensions;
using Handmade.Application.Catalog;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Handmade.Api.Controllers;

[ApiController]
[Route(ApiRoutes.CatalogCategories)]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.CatalogPolicy)]
public sealed class CatalogCategoriesController : ControllerBase
{
    private readonly IPublicCatalogService _catalog;

    public CatalogCategoriesController(IPublicCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Public active category tree.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTreeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryTreeResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _catalog.ListCategoriesAsync(cancellationToken));
    }
}

[ApiController]
[Route(ApiRoutes.CatalogProducts)]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.CatalogPolicy)]
public sealed class CatalogProductsController : ControllerBase
{
    private readonly IPublicCatalogService _catalog;

    public CatalogProductsController(IPublicCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Public published products. Sort: newest, priceAsc, priceDesc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PublicProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PublicProductResponse>>> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? sellerId,
        [FromQuery] string? q,
        [FromQuery] string? sort = CatalogSortOptions.Newest,
        [FromQuery] int page = PagingQuery.DefaultPage,
        [FromQuery] int pageSize = PagingQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagingQuery paging = new() { Page = page, PageSize = pageSize };
        return Ok(await _catalog.ListProductsAsync(categoryId, sellerId, q, sort, paging, cancellationToken));
    }

    /// <summary>Public published product by slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(PublicProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicProductResponse>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        return Ok(await _catalog.GetBySlugAsync(slug, cancellationToken));
    }
}
