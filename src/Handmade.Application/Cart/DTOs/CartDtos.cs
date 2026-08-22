namespace Handmade.Application.Cart.DTOs;

public sealed record AddCartItemRequest(Guid ProductId, Guid? VariantId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartItemResponse(
    Guid ProductId,
    Guid? VariantId,
    string? VariantName,
    string Name,
    string? ImageUrl,
    string SellerName,
    int Quantity,
    decimal UnitPrice,
    string Currency,
    decimal Subtotal,
    bool IsAvailable,
    bool PriceChanged,
    string? UnavailabilityReason);

public sealed record CartResponse(
    Guid? Id,
    IReadOnlyList<CartItemResponse> Items,
    decimal Subtotal,
    decimal Total,
    string? Currency,
    int ItemCount,
    int DistinctItemCount);
