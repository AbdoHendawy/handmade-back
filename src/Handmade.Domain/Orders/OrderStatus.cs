namespace Handmade.Domain.Orders;

public enum OrderStatus
{
    Placed = 0,
    Confirmed = 1,
    Preparing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}
