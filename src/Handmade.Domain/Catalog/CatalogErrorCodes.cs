namespace Handmade.Domain.Catalog;

public static class CatalogErrorCodes
{
    public const string InvalidName = "invalid_catalog_name";
    public const string InvalidSlug = "invalid_catalog_slug";
    public const string InvalidDescription = "invalid_catalog_description";
    public const string InvalidParent = "invalid_category_parent";
    public const string CircularCategory = "circular_category_hierarchy";
    public const string CategoryInactive = "category_inactive";
    public const string CategoryNotFound = "category_not_found";
    public const string DuplicateSlug = "duplicate_slug";
    public const string DuplicateSku = "duplicate_sku";
    public const string InvalidPrice = "invalid_price";
    public const string InvalidCurrency = "invalid_currency";
    public const string InvalidStateTransition = "invalid_product_state";
    public const string ProductNotFound = "product_not_found";
    public const string ProductNotOwned = "product_not_owned";
    public const string ProductNotEditable = "product_not_editable";
    public const string ProductIncomplete = "product_incomplete";
    public const string ImageNotFound = "product_image_not_found";
    public const string VariantNotFound = "product_variant_not_found";
    public const string InvalidStorageKey = "invalid_storage_key";
    public const string InvalidImageFile = "invalid_image_file";
    public const string ImageTooLarge = "image_too_large";
    public const string ImageContentTypeNotAllowed = "image_content_type_not_allowed";
    public const string InvalidSku = "invalid_sku";
    public const string InvalidSortOrder = "invalid_sort_order";
    public const string RejectionReasonRequired = "rejection_reason_required";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string CategoryHasProducts = "category_in_use";
    public const string InvalidImageReorder = "invalid_image_reorder";
    public const string ProductNotPurchasable = "product_not_purchasable";
    public const string SellerNotActive = "seller_not_active";
    public const string VariantRequired = "variant_required";
    public const string InsufficientStock = "insufficient_stock";
    public const string InvalidStockQuantity = "invalid_stock_quantity";
}
