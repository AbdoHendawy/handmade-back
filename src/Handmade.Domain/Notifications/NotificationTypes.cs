namespace Handmade.Domain.Notifications;

/// <summary>
/// Stable notification type codes. Other modules publish these; they are not SignalR contracts.
/// </summary>
public static class NotificationTypes
{
    public const string Welcome = "identity.welcome";

    public const string SellerApplicationSubmitted = "seller.application.submitted";

    public const string SellerApplicationApproved = "seller.application.approved";

    public const string SellerApplicationRejected = "seller.application.rejected";

    public const string SellerSuspended = "seller.suspended";

    public const string SellerReactivated = "seller.reactivated";

    public const string ProductSubmitted = "catalog.product.submitted";

    public const string ProductApproved = "catalog.product.approved";

    public const string ProductRejected = "catalog.product.rejected";

    public const string OrderPlaced = "order.placed";

    public const string OrderReceived = "order.received";

    public const string OrderConfirmed = "order.confirmed";

    public const string OrderPreparing = "order.preparing";

    public const string OrderShipped = "order.shipped";

    public const string OrderDelivered = "order.delivered";

    public const string OrderCancelled = "order.cancelled";
}
