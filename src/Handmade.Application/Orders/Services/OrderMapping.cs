using Handmade.Application.Orders.DTOs;
using Handmade.Domain.Orders;

namespace Handmade.Application.Orders.Services;

internal static class OrderMapping
{
    public static OrderGroupResponse ToGroupResponse(OrderGroup group, IReadOnlyList<Order> orders)
    {
        return new OrderGroupResponse(
            group.Id,
            group.Number,
            group.Status.ToString(),
            group.PaymentMethod.ToString(),
            group.Currency,
            group.Subtotal,
            group.Total,
            group.CreatedAt,
            new OrderCustomerResponse(group.CustomerFirstName, group.CustomerLastName, group.CustomerEmail),
            ToDelivery(group),
            orders.Select(ToOrderResponse).ToList());
    }

    public static OrderGroupListItemResponse ToGroupListItem(OrderGroup group, int orderCount)
    {
        return new OrderGroupListItemResponse(
            group.Id,
            group.Number,
            group.Status.ToString(),
            group.PaymentMethod.ToString(),
            group.Currency,
            group.Subtotal,
            group.Total,
            group.CreatedAt,
            orderCount);
    }

    public static OrderResponse ToOrderResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.OrderGroupId,
            order.Number,
            order.Status.ToString(),
            order.SellerId,
            order.SellerNameSnapshot,
            order.Currency,
            order.Subtotal,
            order.Total,
            order.CreatedAt,
            order.Items.Select(ToItemResponse).ToList());
    }

    public static OrderItemResponse ToItemResponse(OrderItem item)
    {
        return new OrderItemResponse(
            item.Id,
            item.ProductId,
            item.VariantId,
            item.SellerId,
            item.ProductNameSnapshot,
            item.VariantNameSnapshot,
            item.SkuSnapshot,
            item.ImageUrlSnapshot,
            item.Quantity,
            item.UnitPrice,
            item.LineTotal,
            item.Currency);
    }

    private static OrderDeliveryResponse ToDelivery(OrderGroup group)
    {
        return new OrderDeliveryResponse(
            group.RecipientName,
            group.Phone,
            group.AddressLine1,
            group.AddressLine2,
            group.City,
            group.Governorate,
            group.PostalCode,
            group.Notes);
    }
}
