using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Application.Orders.DTOs;

public sealed record CheckoutRequest(
    string RecipientName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Governorate,
    string? PostalCode,
    string? Notes);

public sealed record OrderCustomerResponse(string FirstName, string LastName, string Email);

public sealed record OrderDeliveryResponse(
    string RecipientName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Governorate,
    string? PostalCode,
    string? Notes);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    Guid SellerId,
    string ProductName,
    string? VariantName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency);

public sealed record OrderResponse(
    Guid Id,
    Guid OrderGroupId,
    long Number,
    string Status,
    Guid SellerId,
    string SellerName,
    string Currency,
    decimal Subtotal,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record OrderGroupResponse(
    Guid Id,
    long Number,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal Total,
    DateTimeOffset CreatedAt,
    OrderCustomerResponse Customer,
    OrderDeliveryResponse Delivery,
    IReadOnlyList<OrderResponse> Orders);

public sealed record OrderGroupListItemResponse(
    Guid Id,
    long Number,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal Total,
    DateTimeOffset CreatedAt,
    int OrderCount);

public static class OrderDeliveryFactory
{
    public static OrderDeliverySnapshot FromRequest(CheckoutRequest request)
    {
        return OrderDeliverySnapshot.Create(
            request.RecipientName,
            request.Phone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Governorate,
            request.PostalCode,
            request.Notes);
    }
}
