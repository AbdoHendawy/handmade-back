using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Common;
using Handmade.Application.Orders.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Orders.Services;

public interface ICustomerOrderService
{
    Task<PagedResult<OrderGroupListItemResponse>> ListMineAsync(
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<OrderGroupResponse> GetByIdAsync(Guid orderGroupId, CancellationToken cancellationToken = default);
}

public sealed class CustomerOrderService : ICustomerOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CustomerOrderService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<OrderGroupListItemResponse>> ListMineAsync(
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        IQueryable<OrderGroup> query = _db.OrderGroups
            .AsNoTracking()
            .Where(g => g.CustomerId == userId)
            .OrderByDescending(g => g.CreatedAt);

        int total = await query.CountAsync(cancellationToken);
        List<OrderGroup> groups = await query
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);
        List<Guid> groupIds = groups.Select(g => g.Id).ToList();
        Dictionary<Guid, int> counts = groupIds.Count == 0
            ? []
            : await _db.Orders
                .AsNoTracking()
                .Where(o => groupIds.Contains(o.OrderGroupId))
                .GroupBy(o => o.OrderGroupId)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        return new PagedResult<OrderGroupListItemResponse>(
            groups.Select(g => OrderMapping.ToGroupListItem(g, counts.GetValueOrDefault(g.Id))).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<OrderGroupResponse> GetByIdAsync(
        Guid orderGroupId,
        CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        OrderGroup group = await _db.OrderGroups
                               .AsNoTracking()
                               .FirstOrDefaultAsync(g => g.Id == orderGroupId, cancellationToken)
                           ?? throw NotFound(orderGroupId);
        if (group.CustomerId != userId)
        {
            throw NotFound(orderGroupId);
        }

        List<Order> orders = await LoadOrdersAsync([group.Id], cancellationToken);
        return OrderMapping.ToGroupResponse(group, orders);
    }

    private async Task<List<Order>> LoadOrdersAsync(IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
    {
        List<Order> orders = await _db.Orders
            .AsNoTracking()
            .Where(o => groupIds.Contains(o.OrderGroupId))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
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

        return orders;
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }

    private static NotFoundException NotFound(Guid orderGroupId)
    {
        return new NotFoundException("OrderGroup", orderGroupId) { Code = OrderErrorCodes.OrderNotFound };
    }
}
