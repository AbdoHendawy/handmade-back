using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Orders.DTOs;
using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CartAggregate = Handmade.Domain.Cart.Cart;

namespace Handmade.Application.Orders.Services;

public interface ICheckoutService
{
    Task<OrderGroupResponse> PlaceAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}

public sealed class CheckoutService : ICheckoutService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProductPurchaseQuery _purchase;
    private readonly IProductInventory _inventory;
    private readonly IOrderNotificationService _notifications;
    private readonly IValidator<CheckoutRequest> _validator;
    private readonly IClock _clock;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IProductPurchaseQuery purchase,
        IProductInventory inventory,
        IOrderNotificationService notifications,
        IValidator<CheckoutRequest> validator,
        IClock clock,
        ILogger<CheckoutService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _purchase = purchase;
        _inventory = inventory;
        _notifications = notifications;
        _validator = validator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OrderGroupResponse> PlaceAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_validator], cancellationToken);
        OrderDeliverySnapshot delivery = OrderDeliveryFactory.FromRequest(request);
        Guid userId = RequireUserId();

        for (int attempt = 0; attempt < CheckoutConcurrency.MaxAttempts; attempt++)
        {
            _db.ClearTrackedEntities();
            HashSet<Guid> mutatedProductIds = [];
            HashSet<Guid> mutatedVariantIds = [];
            try
            {
                CheckoutGraph graph = await BuildAndSaveAsync(
                    userId,
                    delivery,
                    mutatedProductIds,
                    mutatedVariantIds,
                    cancellationToken);

                await _notifications.NotifyPlacedAsync(
                    graph.Group,
                    graph.Orders,
                    graph.SellerUserIds,
                    cancellationToken);

                return OrderMapping.ToGroupResponse(graph.Group, graph.Orders);
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
                        "Inventory concurrency during checkout for user {UserId}; rebuilding order graph",
                        userId);
                    continue;
                }

                if (action == CheckoutConcurrencyAction.OrdersConflict)
                {
                    throw new ConflictException("The order could not be placed because inventory changed.")
                    {
                        Code = OrderErrorCodes.ConcurrencyConflict
                    };
                }

                throw;
            }
        }

        throw new ConflictException("The order could not be placed because inventory changed.")
        {
            Code = OrderErrorCodes.ConcurrencyConflict
        };
    }

    private async Task<CheckoutGraph> BuildAndSaveAsync(
        Guid userId,
        OrderDeliverySnapshot delivery,
        HashSet<Guid> mutatedProductIds,
        HashSet<Guid> mutatedVariantIds,
        CancellationToken cancellationToken)
    {
        User customer = await _db.Users
                             .AsNoTracking()
                             .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                         ?? throw new UnauthorizedAccessException("Authentication is required.");

        CartAggregate cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
                             ?? throw EmptyCart();
        List<CartItem> cartItems = await _db.CartItems
            .Where(i => i.CartId == cart.Id)
            .ToListAsync(cancellationToken);
        if (cartItems.Count == 0)
        {
            throw EmptyCart();
        }

        cart.RestoreItems(cartItems);

        IReadOnlyList<ProductPurchaseInfo> live = await _purchase.GetManyForCartAsync(
            cartItems.Select(i => new ProductPurchaseKey(i.ProductId, i.VariantId)).ToList(),
            cancellationToken);
        Dictionary<(Guid ProductId, Guid? VariantId), ProductPurchaseInfo> byKey = live.ToDictionary(
            p => (p.ProductId, p.VariantId));

        string? currency = null;
        List<CheckoutLine> lines = [];
        foreach (CartItem cartItem in cartItems)
        {
            if (!byKey.TryGetValue((cartItem.ProductId, cartItem.VariantId), out ProductPurchaseInfo? info))
            {
                throw new DomainException("A cart line is not available for purchase.")
                {
                    Code = OrderErrorCodes.LineNotPurchasable
                };
            }

            EnsureLinePurchasable(info);
            if (info.AvailableStock < cartItem.Quantity)
            {
                throw new DomainException("Not enough stock for this product.")
                {
                    Code = CatalogErrorCodes.InsufficientStock
                };
            }

            string lineCurrency = CatalogMoney.RequireCurrency(info.Currency);
            CatalogMoney.RequireAmount(info.UnitPrice);
            if (currency is null)
            {
                currency = lineCurrency;
            }
            else if (!string.Equals(currency, lineCurrency, StringComparison.Ordinal))
            {
                throw new DomainException("Checkout lines must share one currency.")
                {
                    Code = OrderErrorCodes.CurrencyMismatch
                };
            }

            lines.Add(new CheckoutLine(cartItem, info));
        }

        DateTimeOffset now = _clock.UtcNow;
        OrderGroup group = OrderGroup.Create(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            delivery,
            currency!,
            now);

        List<Order> orders = [];
        foreach (IGrouping<Guid, CheckoutLine> sellerLines in lines.GroupBy(l => l.Info.SellerId))
        {
            CheckoutLine first = sellerLines.First();
            Order order = Order.Create(
                group.Id,
                customer.Id,
                first.Info.SellerId,
                first.Info.SellerName,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                delivery,
                currency!,
                now);

            foreach (CheckoutLine line in sellerLines)
            {
                order.AddItem(
                    line.Info.ProductId,
                    line.Info.VariantId,
                    line.Info.SellerId,
                    line.Info.Name,
                    line.Info.VariantName,
                    line.Info.Sku,
                    line.Info.ImageUrl,
                    line.Item.Quantity,
                    line.Info.UnitPrice,
                    line.Info.Currency);
            }

            OrderTotals totals = OrderCalculator.ComputeOrder(
                order.Items.Select(i => new OrderLineAmounts(i.UnitPrice, i.Quantity)).ToList());
            order.ApplyTotals(totals.Subtotal, totals.Total);
            orders.Add(order);
        }

        OrderGroupTotals groupTotals = OrderCalculator.ComputeGroup(
            orders.Select(o => new OrderTotals(o.Subtotal, o.Total)).ToList());
        group.ApplyTotals(groupTotals.Subtotal, groupTotals.Total);

        List<StockDecrement> decrements = lines
            .Select(l => new StockDecrement(l.Info.ProductId, l.Info.VariantId, l.Item.Quantity))
            .ToList();
        CaptureMutatedIds(decrements, mutatedProductIds, mutatedVariantIds);
        await _inventory.DecrementAsync(decrements, cancellationToken);

        _db.OrderGroups.Add(group);
        foreach (Order order in orders)
        {
            _db.Orders.Add(order);
            foreach (OrderItem item in order.Items)
            {
                _db.OrderItems.Add(item);
            }
        }

        IReadOnlyList<CartItem> removed = cart.Clear();
        _db.CartItems.RemoveRange(removed);

        await _db.SaveChangesAsync(cancellationToken);

        List<Guid> sellerIds = orders.Select(o => o.SellerId).Distinct().ToList();
        Dictionary<Guid, Guid> sellerUserIds = await _db.SellerProfiles
            .AsNoTracking()
            .Where(s => sellerIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.UserId, cancellationToken);

        return new CheckoutGraph(group, orders, sellerUserIds);
    }

    private static void EnsureLinePurchasable(ProductPurchaseInfo info)
    {
        if (info.IsPurchasable)
        {
            return;
        }

        throw new DomainException("A cart line is not available for purchase.")
        {
            Code = OrderErrorCodes.LineNotPurchasable
        };
    }

    private static void CaptureMutatedIds(
        IReadOnlyList<StockDecrement> decrements,
        HashSet<Guid> mutatedProductIds,
        HashSet<Guid> mutatedVariantIds)
    {
        foreach (StockDecrement decrement in decrements)
        {
            if (decrement.VariantId is Guid variantId)
            {
                mutatedVariantIds.Add(variantId);
            }
            else
            {
                mutatedProductIds.Add(decrement.ProductId);
            }
        }
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }

    private static DomainException EmptyCart()
    {
        return new DomainException("The cart is empty.") { Code = OrderErrorCodes.CartEmpty };
    }

    private sealed record CheckoutLine(CartItem Item, ProductPurchaseInfo Info);

    private sealed record CheckoutGraph(
        OrderGroup Group,
        IReadOnlyList<Order> Orders,
        IReadOnlyDictionary<Guid, Guid> SellerUserIds);
}
