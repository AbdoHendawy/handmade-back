using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Catalog.Services;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Orders.Services;

public interface IOrderCancellationService
{
    Task<Order> PersistCancelAsync(
        Guid orderId,
        Func<Order, bool> isOwner,
        CancellationToken cancellationToken = default);
}

public sealed class OrderCancellationService : IOrderCancellationService
{
    private readonly IApplicationDbContext _db;
    private readonly IProductInventory _inventory;
    private readonly IClock _clock;
    private readonly ILogger<OrderCancellationService> _logger;

    public OrderCancellationService(
        IApplicationDbContext db,
        IProductInventory inventory,
        IClock clock,
        ILogger<OrderCancellationService> logger)
    {
        _db = db;
        _inventory = inventory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Order> PersistCancelAsync(
        Guid orderId,
        Func<Order, bool> isOwner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isOwner);

        for (int attempt = 0; attempt < CheckoutConcurrency.MaxAttempts; attempt++)
        {
            _db.ClearTrackedEntities();
            HashSet<Guid> mutatedProductIds = [];
            HashSet<Guid> mutatedVariantIds = [];
            try
            {
                return await CancelAndSaveAsync(
                    orderId,
                    isOwner,
                    mutatedProductIds,
                    mutatedVariantIds,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                CheckoutConcurrencyAction action = CheckoutConcurrency.Decide(
                    attempt,
                    exception,
                    mutatedProductIds,
                    mutatedVariantIds);

                if (action == CheckoutConcurrencyAction.Retry)
                {
                    _logger.LogWarning(
                        exception,
                        "Inventory concurrency during cancellation for order {OrderId}; rebuilding cancellation attempt",
                        orderId);
                    continue;
                }

                if (action == CheckoutConcurrencyAction.OrdersConflict)
                {
                    throw InventoryConflict();
                }

                throw;
            }
        }

        throw InventoryConflict();
    }

    private async Task<Order> CancelAndSaveAsync(
        Guid orderId,
        Func<Order, bool> isOwner,
        HashSet<Guid> mutatedProductIds,
        HashSet<Guid> mutatedVariantIds,
        CancellationToken cancellationToken)
    {
        Order order = await _db.Orders
                          .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                      ?? throw NotFound(orderId);
        if (!isOwner(order))
        {
            throw NotFound(orderId);
        }

        order.Cancel(_clock.UtcNow);

        List<OrderItem> items = await _db.OrderItems
            .AsNoTracking()
            .Where(i => i.OrderId == order.Id)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        List<StockIncrement> lines = items
            .Select(item => new StockIncrement(item.ProductId, item.VariantId, item.Quantity))
            .ToList();
        CaptureMutatedIds(lines, mutatedProductIds, mutatedVariantIds);
        await _inventory.IncrementAsync(lines, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return order;
    }

    private static void CaptureMutatedIds(
        IReadOnlyList<StockIncrement> lines,
        HashSet<Guid> mutatedProductIds,
        HashSet<Guid> mutatedVariantIds)
    {
        foreach (StockIncrement line in lines)
        {
            if (line.VariantId is Guid variantId)
            {
                mutatedVariantIds.Add(variantId);
            }
            else
            {
                mutatedProductIds.Add(line.ProductId);
            }
        }
    }

    private static ConflictException InventoryConflict()
    {
        return new ConflictException("The order could not be cancelled because inventory changed.")
        {
            Code = OrderErrorCodes.ConcurrencyConflict
        };
    }

    private static NotFoundException NotFound(Guid orderId)
    {
        return new NotFoundException("Order", orderId) { Code = OrderErrorCodes.OrderNotFound };
    }
}
