using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Behaviors;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Catalog.Services;
using Handmade.Domain.Cart;
using Handmade.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CartAggregate = Handmade.Domain.Cart.Cart;

namespace Handmade.Application.Cart.Services;

public interface ICartService
{
    Task<CartResponse> GetMineAsync(CancellationToken cancellationToken = default);

    Task<CartResponse> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default);

    Task<CartResponse> UpdateItemAsync(
        Guid productId,
        Guid? variantId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveItemAsync(Guid productId, Guid? variantId, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class CartService : ICartService
{
    private const int MaxConcurrencyAttempts = 2;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProductPurchaseQuery _purchase;
    private readonly IValidator<AddCartItemRequest> _addValidator;
    private readonly IValidator<UpdateCartItemRequest> _updateValidator;
    private readonly ILogger<CartService> _logger;

    public CartService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IProductPurchaseQuery purchase,
        IValidator<AddCartItemRequest> addValidator,
        IValidator<UpdateCartItemRequest> updateValidator,
        ILogger<CartService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _purchase = purchase;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<CartResponse> GetMineAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        CartAggregate? cart = await _db.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart is null)
        {
            return CartMapping.Empty();
        }

        List<CartItem> items = await _db.CartItems
            .AsNoTracking()
            .Where(i => i.CartId == cart.Id)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return await MapAsync(cart.Id, items, cancellationToken);
    }

    public async Task<CartResponse> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_addValidator], cancellationToken);
        Guid userId = RequireUserId();
        ProductPurchaseInfo purchase = await RequirePurchasableAsync(request.ProductId, request.VariantId, cancellationToken);
        return await SaveWithRetryAsync(
            userId,
            async () =>
            {
                CartAggregate cart = await LoadOrCreateCartAsync(userId, cancellationToken);
                HashSet<Guid> existingIds = cart.Items.Select(i => i.Id).ToHashSet();
                CartItem item = cart.AddOrIncrease(
                    purchase.ProductId,
                    purchase.VariantId,
                    request.Quantity,
                    purchase.UnitPrice,
                    purchase.Currency);
                if (!existingIds.Contains(item.Id))
                {
                    _db.CartItems.Add(item);
                }

                await CartPersistence.SaveChangesAsync(_db, cancellationToken);
                return await MapTrackedAsync(cart, cancellationToken);
            },
            cancellationToken);
    }

    public async Task<CartResponse> UpdateItemAsync(
        Guid productId,
        Guid? variantId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);
        Guid userId = RequireUserId();
        ProductPurchaseInfo purchase = await RequirePurchasableAsync(productId, variantId, cancellationToken);
        return await SaveWithRetryAsync(
            userId,
            async () =>
            {
                CartAggregate cart = await LoadOwnedCartAsync(userId, cancellationToken)
                                     ?? throw new NotFoundException("CartItem", productId);
                CartItem item = cart.UpdateQuantity(purchase.ProductId, purchase.VariantId, request.Quantity);
                item.ReplacePriceSnapshot(purchase.UnitPrice, purchase.Currency);
                await CartPersistence.SaveChangesAsync(_db, cancellationToken);
                return await MapTrackedAsync(cart, cancellationToken);
            },
            cancellationToken);
    }

    public async Task RemoveItemAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        await SaveWithRetryAsync(
            userId,
            async () =>
            {
                CartAggregate cart = await LoadOwnedCartAsync(userId, cancellationToken)
                                     ?? throw new NotFoundException("CartItem", productId);
                CartItem removed = cart.RemoveItem(productId, variantId);
                _db.CartItems.Remove(removed);
                await CartPersistence.SaveChangesAsync(_db, cancellationToken);
                return CartMapping.Empty();
            },
            cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = RequireUserId();
        CartAggregate? cart = await LoadOwnedCartAsync(userId, cancellationToken);
        if (cart is null)
        {
            return;
        }

        IReadOnlyList<CartItem> removed = cart.Clear();
        _db.CartItems.RemoveRange(removed);
        await CartPersistence.SaveChangesAsync(_db, cancellationToken);
    }

    private async Task<CartResponse> SaveWithRetryAsync(
        Guid userId,
        Func<Task<CartResponse>> action,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxConcurrencyAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (ConflictException exception) when (
                exception.Code == CartErrorCodes.ConcurrencyConflict && attempt < MaxConcurrencyAttempts - 1)
            {
                _logger.LogWarning("Cart concurrency conflict for user {UserId}, retrying", userId);
                _db.ClearTrackedEntities();
            }
        }

        throw new ConflictException("The cart was modified by another request.")
        {
            Code = CartErrorCodes.ConcurrencyConflict
        };
    }

    private async Task<ProductPurchaseInfo> RequirePurchasableAsync(
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _purchase.GetForPurchaseAsync(productId, variantId, cancellationToken);
        }
        catch (Exception exception) when (exception is DomainException or NotFoundException)
        {
            _logger.LogWarning(
                exception,
                "Invalid cart purchase attempt for product {ProductId} variant {VariantId}",
                productId,
                variantId);
            throw;
        }
    }

    private async Task<CartAggregate> LoadOrCreateCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        CartAggregate? cart = await LoadOwnedCartAsync(userId, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = CartAggregate.Create(userId);
        _db.Carts.Add(cart);
        return cart;
    }

    private async Task<CartAggregate?> LoadOwnedCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        CartAggregate? cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart is null)
        {
            return null;
        }

        List<CartItem> items = await _db.CartItems
            .Where(i => i.CartId == cart.Id)
            .ToListAsync(cancellationToken);
        cart.RestoreItems(items);
        return cart;
    }

    private async Task<CartResponse> MapTrackedAsync(CartAggregate cart, CancellationToken cancellationToken)
    {
        return await MapAsync(cart.Id, cart.Items.OrderBy(i => i.CreatedAt).ToList(), cancellationToken);
    }

    private async Task<CartResponse> MapAsync(
        Guid cartId,
        IReadOnlyList<CartItem> items,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductPurchaseInfo> products = await _purchase.GetManyForCartAsync(
            items.Select(i => new ProductPurchaseKey(i.ProductId, i.VariantId)).ToList(),
            cancellationToken);
        Dictionary<(Guid ProductId, Guid? VariantId), ProductPurchaseInfo> byKey = products.ToDictionary(
            p => (p.ProductId, p.VariantId));
        return CartMapping.ToResponse(cartId, items, byKey);
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return _currentUser.UserId.Value;
    }
}
