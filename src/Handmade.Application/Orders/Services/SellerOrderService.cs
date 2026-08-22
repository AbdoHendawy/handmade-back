using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Common;
using Handmade.Application.Orders.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Orders.Services;

public interface ISellerOrderService
{
    Task<PagedResult<OrderResponse>> ListMineAsync(
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> GetMineAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public sealed class SellerOrderService : ISellerOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SellerOrderService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<OrderResponse>> ListMineAsync(
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        SellerProfile seller = await RequireActiveSellerAsync(cancellationToken);
        IQueryable<Order> query = _db.Orders
            .AsNoTracking()
            .Where(o => o.SellerId == seller.Id)
            .OrderByDescending(o => o.CreatedAt);

        int total = await query.CountAsync(cancellationToken);
        List<Order> orders = await query
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);
        await RestoreItemsAsync(orders, cancellationToken);

        return new PagedResult<OrderResponse>(
            orders.Select(OrderMapping.ToOrderResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<OrderResponse> GetMineAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        SellerProfile seller = await RequireActiveSellerAsync(cancellationToken);
        Order order = await _db.Orders
                          .AsNoTracking()
                          .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                      ?? throw NotFound(orderId);
        if (order.SellerId != seller.Id)
        {
            throw NotFound(orderId);
        }

        await RestoreItemsAsync([order], cancellationToken);
        return OrderMapping.ToOrderResponse(order);
    }

    private async Task RestoreItemsAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        List<Guid> orderIds = orders.Select(o => o.Id).ToList();
        List<OrderItem> items = orderIds.Count == 0
            ? []
            : await _db.OrderItems
                .AsNoTracking()
                .Where(i => orderIds.Contains(i.OrderId))
                .OrderBy(i => i.CreatedAt)
                .ToListAsync(cancellationToken);
        ILookup<Guid, OrderItem> itemsByOrder = items.ToLookup(i => i.OrderId);
        foreach (Order order in orders)
        {
            order.RestoreItems(itemsByOrder[order.Id]);
        }
    }

    private async Task<SellerProfile> RequireActiveSellerAsync(CancellationToken cancellationToken)
    {
        Guid userId = RequireUserId();
        SellerProfile seller = await _db.SellerProfiles
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                               ?? throw new ForbiddenException("An active seller profile is required.")
                               {
                                   Code = SellerErrorCodes.ProfileNotActive
                               };

        if (!seller.IsActive)
        {
            throw new ForbiddenException("An active seller profile is required.")
            {
                Code = SellerErrorCodes.ProfileNotActive
            };
        }

        return seller;
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }

    private static NotFoundException NotFound(Guid orderId)
    {
        return new NotFoundException("Order", orderId) { Code = OrderErrorCodes.OrderNotFound };
    }
}
