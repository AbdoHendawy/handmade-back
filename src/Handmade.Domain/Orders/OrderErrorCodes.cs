namespace Handmade.Domain.Orders;

public static class OrderErrorCodes
{
    public const string CartEmpty = "cart_empty";
    public const string LineNotPurchasable = "line_not_purchasable";
    public const string InvalidPrice = "invalid_price";
    public const string CurrencyMismatch = "currency_mismatch";
    public const string OrderNotFound = "order_not_found";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string InvalidQuantity = "invalid_quantity";
    public const string SellerMismatch = "seller_mismatch";
    public const string InvalidStatusTransition = "invalid_status_transition";
}
