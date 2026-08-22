namespace Handmade.Domain.Cart;

public static class CartErrorCodes
{
    public const string InvalidQuantity = "invalid_quantity";
    public const string ProductNotPurchasable = "product_not_purchasable";
    public const string SellerNotActive = "seller_not_active";
    public const string VariantRequired = "variant_required";
    public const string CurrencyMismatch = "currency_mismatch";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string CartItemNotFound = "cart_item_not_found";
}
