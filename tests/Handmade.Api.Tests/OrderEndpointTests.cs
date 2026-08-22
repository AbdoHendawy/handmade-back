using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Cart.DTOs;
using Handmade.Application.Catalog.DTOs;
using Handmade.Application.Common;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Notifications.DTOs;
using Handmade.Application.Orders.DTOs;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class OrderEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly CheckoutRequest Delivery = new(
        "Nour Hassan",
        "+201001234567",
        "12 Nile Street",
        "Apt 4",
        "Cairo",
        "Cairo",
        "11511",
        "Leave at the door");

    private readonly HandmadeApiFactory _factory;

    public OrderEndpointTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_RequiresAuthentication()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/checkout", Delivery)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/orders")).StatusCode);
        Guid orderId = Guid.CreateVersion7();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsync($"/api/v1/orders/{orderId}/cancel", null)).StatusCode);
        foreach (string action in new[] { "confirm", "prepare", "ship", "deliver", "cancel" })
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.PostAsync($"/api/v1/seller/orders/{orderId}/{action}", null)).StatusCode);
        }
    }

    [Fact]
    public void PaymentMethod_IsMappedAsRequiredStringOnOrderGroup()
    {
        _ = _factory.CreateMigratedClient();
        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        IProperty property = db.Model
            .FindEntityType(typeof(OrderGroup))!
            .FindProperty(nameof(OrderGroup.PaymentMethod))!;

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(PaymentMethod), property.ClrType);
        Assert.Equal(32, property.GetMaxLength());
        Assert.Equal("character varying(32)", property.GetColumnType());
        Assert.Null(db.Model.FindEntityType(typeof(Order))!.FindProperty("PaymentMethod"));
    }

    [Fact]
    public async Task OnlinePaymentEndpoints_DoNotExist()
    {
        HttpClient client = _factory.CreateMigratedClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/payments", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/checkout/payment", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/payment/webhook", null)).StatusCode);
    }

    [Fact]
    public async Task EmptyCart_ReturnsCartEmpty_AndPersistsNothing()
    {
        HttpClient client = _factory.CreateMigratedClient();
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderErrorCodes.CartEmpty, await ReadCodeAsync(response));

        PagedResult<OrderGroupListItemResponse> page = (await (await client.GetAsync("/api/v1/orders")).Content
            .ReadFromJsonAsync<PagedResult<OrderGroupListItemResponse>>(JsonOptions))!;
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Checkout_SplitsSellers_UsesLivePrice_AndKeepsCartRow()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct first = await PublishProductAsync(client, 100m, stock: 5);
        PublishedProduct second = await PublishProductAsync(client, 70m, stock: 5);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, first.Id, 2);
        await AddAsync(client, second.Id, 1);

        Authorize(client, first.SellerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{first.Id}",
                new UpdateProductRequest(
                    first.Name,
                    "Handmade leather bracelet with a brass clasp.",
                    first.CategoryId,
                    120m,
                    "EGP",
                    null,
                    5))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage placed = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.NotNull(placed.Headers.Location);
        OrderGroupResponse group = (await placed.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(2, group.Orders.Count);
        Assert.Equal(310m, group.Subtotal);
        Assert.Equal(group.Subtotal, group.Total);
        Assert.Equal("Placed", group.Status);
        Assert.Equal("CashOnDelivery", group.PaymentMethod);
        Assert.True(group.Number > 0);
        Assert.Contains(group.Orders, o => o.SellerId == first.SellerId && o.Items[0].UnitPrice == 120m);
        Assert.Contains(group.Orders, o => o.SellerId == second.SellerId);
        Assert.All(group.Orders, o => Assert.Equal("Placed", o.Status));
        Assert.DoesNotContain(group.Orders, o => o.Items.Select(i => i.SellerId).Distinct().Count() > 1);
        Assert.Contains(group.Id.ToString(), placed.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        CartResponse cart = (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
        Assert.Empty(cart.Items);
        Assert.NotNull(cart.Id);

        OrderGroupResponse loaded = (await (await client.GetAsync($"/api/v1/orders/{group.Id}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(group.Id, loaded.Id);
        Assert.Equal("CashOnDelivery", loaded.PaymentMethod);
        Assert.Equal(120m, loaded.Orders.Single(o => o.SellerId == first.SellerId).Items[0].UnitPrice);

        Authorize(client, first.SellerToken);
        PagedResult<OrderResponse> sellerPage = (await (await client.GetAsync("/api/v1/seller/orders")).Content
            .ReadFromJsonAsync<PagedResult<OrderResponse>>(JsonOptions))!;
        Assert.Contains(sellerPage.Items, o => o.SellerId == first.SellerId);
        Assert.DoesNotContain(sellerPage.Items, o => o.SellerId == second.SellerId);

        Authorize(client, customer.AccessToken);
        PagedResult<NotificationResponse> inbox = (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
        Assert.Contains(inbox.Items, n => n.Type == NotificationTypes.OrderPlaced);
    }

    [Fact]
    public async Task Snapshots_SurviveCatalogEdits()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 40m, stock: 3, withVariant: true);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 1, product.VariantId);

        OrderGroupResponse group = await CheckoutAsync(client);
        OrderItemResponse item = Assert.Single(group.Orders.SelectMany(o => o.Items));
        Assert.Equal(product.VariantId, item.VariantId);
        Assert.False(string.IsNullOrWhiteSpace(item.Sku));
        string originalName = item.ProductName;

        Authorize(client, product.SellerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}",
                new UpdateProductRequest(
                    "Renamed After Order",
                    "Handmade leather bracelet with a brass clasp.",
                    product.CategoryId,
                    99m,
                    "EGP",
                    null,
                    3))).StatusCode);

        Authorize(client, customer.AccessToken);
        OrderGroupResponse loaded = (await (await client.GetAsync($"/api/v1/orders/{group.Id}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal(originalName, loaded.Orders[0].Items[0].ProductName);
        Assert.Equal(40m, loaded.Orders[0].Items[0].UnitPrice);
    }

    [Fact]
    public async Task FailedCheckout_LeavesCartUnchanged()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 25m, stock: 1);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 2);

        HttpResponseMessage stock = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, stock.StatusCode);
        Assert.Equal("insufficient_stock", await ReadCodeAsync(stock));
        Assert.Single((await GetCartAsync(client)).Items);

        Authorize(client, product.AdminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{product.SellerId}/suspend",
                new SuspendSellerRequest("Policy violation requires a temporary pause."))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage inactive = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(inactive));
        Assert.Single((await GetCartAsync(client)).Items);
    }

    [Fact]
    public async Task UnpublishedAndMissingVariant_AreRejected()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct unpublished = await PublishProductAsync(client, 30m, stock: 4);

        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, unpublished.Id, 1);
        HttpResponseMessage archive = await ArchiveAsync(client, unpublished);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage unpublishedResponse = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, unpublishedResponse.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(unpublishedResponse));

        await client.DeleteAsync("/api/v1/cart");
        PublishedProduct needsVariant = await PublishProductAsync(client, 55m, stock: 4);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, needsVariant.Id, 1);
        Authorize(client, needsVariant.SellerToken);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{needsVariant.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "ORD-" + Guid.NewGuid().ToString("N")[..8],
                    55m,
                    "EGP",
                    4))).StatusCode);

        Authorize(client, customer.AccessToken);
        HttpResponseMessage missingVariant = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.BadRequest, missingVariant.StatusCode);
        Assert.Equal(OrderErrorCodes.LineNotPurchasable, await ReadCodeAsync(missingVariant));
        Assert.Single((await GetCartAsync(client)).Items);
    }

    [Fact]
    public async Task CrossAccess_IsNotFound()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 15m, stock: 2);
        AuthenticationResponse owner = await RegisterAsync(client);
        AuthenticationResponse other = await RegisterAsync(client);
        Authorize(client, owner.AccessToken);
        await AddAsync(client, product.Id, 1);
        OrderGroupResponse group = await CheckoutAsync(client);

        Authorize(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/orders/{group.Id}")).StatusCode);

        Guid orderId = group.Orders[0].Id;
        PublishedProduct otherSellerProduct = await PublishProductAsync(client, 18m, stock: 2);
        Authorize(client, otherSellerProduct.SellerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/seller/orders/{orderId}")).StatusCode);

        Authorize(client, product.SellerToken);
        OrderResponse sellerOrder = (await (await client.GetAsync($"/api/v1/seller/orders/{orderId}")).Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Equal(orderId, sellerOrder.Id);
    }

    [Fact]
    public async Task Seller_CanWalkLifecycle_AndStatusPersists()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Guid orderId = placed.Order.Id;
        Authorize(client, placed.Product.SellerToken);

        Assert.Equal("Confirmed", (await TransitionAsync(client, orderId, "confirm")).Status);
        Assert.Equal("Confirmed", (await GetSellerOrderAsync(client, orderId)).Status);
        Assert.Equal("Preparing", (await TransitionAsync(client, orderId, "prepare")).Status);
        Assert.Equal("Shipped", (await TransitionAsync(client, orderId, "ship")).Status);
        Assert.Equal("Delivered", (await TransitionAsync(client, orderId, "deliver")).Status);
        Assert.Equal("Delivered", (await GetSellerOrderAsync(client, orderId)).Status);

        Authorize(client, placed.CustomerToken);
        OrderGroupResponse group = (await (await client.GetAsync($"/api/v1/orders/{placed.GroupId}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("Placed", group.Status);
        Assert.Equal("Delivered", Assert.Single(group.Orders).Status);
    }

    [Fact]
    public async Task MultiSeller_OrdersAdvanceIndependently_GroupStaysPlaced()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedMultiSellerOrder placed = await PlaceMultiSellerOrderAsync(client);

        Authorize(client, placed.First.SellerToken);
        await TransitionAsync(client, placed.FirstOrder.Id, "confirm");
        await TransitionAsync(client, placed.FirstOrder.Id, "prepare");
        Assert.Equal("Shipped", (await TransitionAsync(client, placed.FirstOrder.Id, "ship")).Status);

        Authorize(client, placed.Second.SellerToken);
        await TransitionAsync(client, placed.SecondOrder.Id, "confirm");
        Assert.Equal("Preparing", (await TransitionAsync(client, placed.SecondOrder.Id, "prepare")).Status);

        Authorize(client, placed.CustomerToken);
        OrderGroupResponse group = (await (await client.GetAsync($"/api/v1/orders/{placed.GroupId}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("Placed", group.Status);
        Assert.Equal("Shipped", group.Orders.Single(o => o.Id == placed.FirstOrder.Id).Status);
        Assert.Equal("Preparing", group.Orders.Single(o => o.Id == placed.SecondOrder.Id).Status);

        Authorize(client, placed.First.SellerToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/seller/orders/{placed.SecondOrder.Id}/ship", null)).StatusCode);
        Assert.Equal("Shipped", (await GetSellerOrderAsync(client, placed.FirstOrder.Id)).Status);

        Authorize(client, placed.Second.SellerToken);
        Assert.Equal("Preparing", (await GetSellerOrderAsync(client, placed.SecondOrder.Id)).Status);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/seller/orders/{placed.FirstOrder.Id}")).StatusCode);
    }

    [Fact]
    public async Task MultiSeller_CustomerCancelOneOrder_LeavesSiblingUnchanged()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedMultiSellerOrder placed = await PlaceMultiSellerOrderAsync(client);

        Authorize(client, placed.Second.SellerToken);
        await TransitionAsync(client, placed.SecondOrder.Id, "confirm");
        await TransitionAsync(client, placed.SecondOrder.Id, "prepare");

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync($"/api/v1/orders/{placed.FirstOrder.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        OrderGroupResponse group = (await (await client.GetAsync($"/api/v1/orders/{placed.GroupId}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("Placed", group.Status);
        Assert.Equal("Cancelled", group.Orders.Single(o => o.Id == placed.FirstOrder.Id).Status);
        Assert.Equal("Preparing", group.Orders.Single(o => o.Id == placed.SecondOrder.Id).Status);

        Authorize(client, placed.First.SellerToken);
        Assert.Equal("Cancelled", (await GetSellerOrderAsync(client, placed.FirstOrder.Id)).Status);

        Authorize(client, placed.Second.SellerToken);
        Assert.Equal("Preparing", (await GetSellerOrderAsync(client, placed.SecondOrder.Id)).Status);
    }

    [Fact]
    public async Task Seller_LifecycleTransitions_NotifyCustomerAfterSave()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Guid orderId = placed.Order.Id;
        Authorize(client, placed.Product.SellerToken);

        await TransitionAsync(client, orderId, "confirm");
        await TransitionAsync(client, orderId, "prepare");
        await TransitionAsync(client, orderId, "ship");
        await TransitionAsync(client, orderId, "deliver");

        Authorize(client, placed.CustomerToken);
        PagedResult<NotificationResponse> customerInbox = await GetInboxAsync(client);
        Assert.Contains(customerInbox.Items, n => n.Type == NotificationTypes.OrderPlaced);
        Assert.Contains(customerInbox.Items, n => n.Type == NotificationTypes.OrderConfirmed);
        Assert.Contains(customerInbox.Items, n => n.Type == NotificationTypes.OrderPreparing);
        Assert.Contains(customerInbox.Items, n => n.Type == NotificationTypes.OrderShipped);
        Assert.Contains(customerInbox.Items, n => n.Type == NotificationTypes.OrderDelivered);
        Assert.DoesNotContain(customerInbox.Items, n => n.Type == NotificationTypes.OrderReceived);

        Authorize(client, placed.Product.SellerToken);
        PagedResult<NotificationResponse> sellerInbox = await GetInboxAsync(client);
        Assert.Contains(sellerInbox.Items, n => n.Type == NotificationTypes.OrderReceived);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == NotificationTypes.OrderConfirmed);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == NotificationTypes.OrderPreparing);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == NotificationTypes.OrderShipped);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == NotificationTypes.OrderDelivered);
    }

    [Fact]
    public async Task Seller_Cancel_NotifiesCustomer()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.Product.SellerToken);
        await TransitionAsync(client, placed.Order.Id, "cancel");

        Authorize(client, placed.CustomerToken);
        PagedResult<NotificationResponse> customerInbox = await GetInboxAsync(client);
        NotificationResponse cancelled = Assert.Single(
            customerInbox.Items,
            n => n.Type == NotificationTypes.OrderCancelled);
        Assert.Contains(placed.Order.Id.ToString("D"), cancelled.DataJson, StringComparison.OrdinalIgnoreCase);

        Authorize(client, placed.Product.SellerToken);
        PagedResult<NotificationResponse> sellerInbox = await GetInboxAsync(client);
        Assert.DoesNotContain(sellerInbox.Items, n => n.Type == NotificationTypes.OrderCancelled);
    }

    [Fact]
    public async Task Customer_Cancel_NotifiesSeller()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.CustomerToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/orders/{placed.Order.Id}/cancel", null)).StatusCode);

        PagedResult<NotificationResponse> customerInbox = await GetInboxAsync(client);
        Assert.DoesNotContain(customerInbox.Items, n => n.Type == NotificationTypes.OrderCancelled);

        Authorize(client, placed.Product.SellerToken);
        PagedResult<NotificationResponse> sellerInbox = await GetInboxAsync(client);
        NotificationResponse cancelled = Assert.Single(
            sellerInbox.Items,
            n => n.Type == NotificationTypes.OrderCancelled);
        Assert.Contains(placed.Order.Id.ToString("D"), cancelled.DataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seller_CanCancelOwnPlacedOrder()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.Product.SellerToken);

        OrderResponse cancelled = await TransitionAsync(client, placed.Order.Id, "cancel");
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Cancelled", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Fact]
    public async Task InvalidSellerTransition_ReturnsConflict()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.Product.SellerToken);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/seller/orders/{placed.Order.Id}/ship",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(response));
        Assert.Equal("Placed", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("prepare")]
    [InlineData("ship")]
    [InlineData("deliver")]
    [InlineData("cancel")]
    public async Task SellerTransition_UnknownAndCrossSeller_AreNotFound(string action)
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        PublishedProduct otherSeller = await PublishProductAsync(client, 18m, stock: 2);

        Authorize(client, placed.Product.SellerToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/seller/orders/{Guid.CreateVersion7()}/{action}", null)).StatusCode);

        Authorize(client, otherSeller.SellerToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/seller/orders/{placed.Order.Id}/{action}", null)).StatusCode);
        Authorize(client, placed.Product.SellerToken);
        Assert.Equal("Placed", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("prepare")]
    [InlineData("ship")]
    [InlineData("deliver")]
    [InlineData("cancel")]
    public async Task InactiveSeller_CannotTransitionOrder(string action)
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);

        Authorize(client, placed.Product.AdminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/v1/admin/sellers/{placed.Product.SellerId}/suspend",
                new SuspendSellerRequest("Policy violation"))).StatusCode);

        Authorize(client, placed.Product.SellerToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/v1/seller/orders/{placed.Order.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/seller/orders/{placed.Order.Id}/{action}", null)).StatusCode);
    }

    [Fact]
    public async Task StaleOrderXmin_IsConcurrencyConflict()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);

        using IServiceScope firstScope = _factory.Services.CreateScope();
        using IServiceScope secondScope = _factory.Services.CreateScope();
        HandmadeDbContext firstDb = firstScope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        HandmadeDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Order first = await firstDb.Orders.SingleAsync(o => o.Id == placed.Order.Id);
        Order second = await secondDb.Orders.SingleAsync(o => o.Id == placed.Order.Id);

        first.Confirm(DateTimeOffset.UtcNow);
        await firstDb.SaveChangesAsync();
        second.Confirm(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());
    }

    [Fact]
    public async Task Customer_CanCancelOwnPlacedOrder()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.CustomerToken);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.Order.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        OrderResponse cancelled = (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Equal("Cancelled", cancelled.Status);

        Authorize(client, placed.Product.SellerToken);
        Assert.Equal("Cancelled", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);

        Authorize(client, placed.CustomerToken);
        OrderGroupResponse group = (await (await client.GetAsync($"/api/v1/orders/{placed.GroupId}")).Content
            .ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("Placed", group.Status);
        Assert.Equal("Cancelled", Assert.Single(group.Orders).Status);
    }

    [Fact]
    public async Task CustomerCancel_UnknownAndOtherCustomer_AreNotFound()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        AuthenticationResponse other = await RegisterAsync(client);

        Authorize(client, placed.CustomerToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/orders/{Guid.CreateVersion7()}/cancel", null)).StatusCode);

        Authorize(client, other.AccessToken);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/orders/{placed.Order.Id}/cancel", null)).StatusCode);

        Authorize(client, placed.Product.SellerToken);
        Assert.Equal("Placed", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("confirm,prepare")]
    [InlineData("confirm,prepare,ship")]
    [InlineData("confirm,prepare,ship,deliver")]
    public async Task CustomerCannotCancelAfterSellerAdvanced(string sellerActions)
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.Product.SellerToken);
        foreach (string action in sellerActions.Split(','))
        {
            await TransitionAsync(client, placed.Order.Id, action);
        }

        Authorize(client, placed.CustomerToken);
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/orders/{placed.Order.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderErrorCodes.InvalidStatusTransition, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task SellerCannotCancelThroughCustomerEndpoint()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.Product.SellerToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/v1/orders/{placed.Order.Id}/cancel", null)).StatusCode);
        Assert.Equal("Placed", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Fact]
    public async Task CustomerCannotUseSellerLifecycleCommands()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);
        Authorize(client, placed.CustomerToken);

        foreach (string action in new[] { "confirm", "prepare", "ship", "deliver" })
        {
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await client.PostAsync($"/api/v1/orders/{placed.Order.Id}/{action}", null)).StatusCode);
        }

        Authorize(client, placed.Product.SellerToken);
        Assert.Equal("Placed", (await GetSellerOrderAsync(client, placed.Order.Id)).Status);
    }

    [Fact]
    public async Task StaleCustomerCancel_ThrowsDbUpdateConcurrencyException()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PlacedSellerOrder placed = await PlaceSellerOrderAsync(client);

        using IServiceScope firstScope = _factory.Services.CreateScope();
        using IServiceScope secondScope = _factory.Services.CreateScope();
        HandmadeDbContext firstDb = firstScope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        HandmadeDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Order first = await firstDb.Orders.SingleAsync(o => o.Id == placed.Order.Id);
        Order second = await secondDb.Orders.SingleAsync(o => o.Id == placed.Order.Id);

        first.Cancel(DateTimeOffset.UtcNow);
        await firstDb.SaveChangesAsync();
        second.Cancel(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentCheckouts_DoNotOversell()
    {
        HttpClient client = _factory.CreateMigratedClient();
        PublishedProduct product = await PublishProductAsync(client, 10m, stock: 1);
        AuthenticationResponse firstUser = await RegisterAsync(client);
        AuthenticationResponse secondUser = await RegisterAsync(client);

        HttpClient first = _factory.CreateClient();
        HttpClient second = _factory.CreateClient();
        Authorize(first, firstUser.AccessToken);
        Authorize(second, secondUser.AccessToken);
        await AddAsync(first, product.Id, 1);
        await AddAsync(second, product.Id, 1);

        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/checkout", Delivery),
            second.PostAsJsonAsync("/api/v1/checkout", Delivery));

        int created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        int rejected = responses.Count(r => r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        Assert.Equal(1, created);
        Assert.Equal(1, rejected);
        HttpResponseMessage failed = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        string? code = await ReadCodeAsync(failed);
        Assert.True(
            code is "insufficient_stock" or OrderErrorCodes.ConcurrencyConflict,
            code);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(HttpClient client, PublishedProduct product)
    {
        Authorize(client, product.AdminToken);
        return await client.PostAsync($"/api/v1/admin/products/{product.Id}/archive", null);
    }

    private static async Task<OrderGroupResponse> CheckoutAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/checkout", Delivery);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderGroupResponse group = (await response.Content.ReadFromJsonAsync<OrderGroupResponse>(JsonOptions))!;
        Assert.Equal("CashOnDelivery", group.PaymentMethod);
        return group;
    }

    private async Task<PlacedSellerOrder> PlaceSellerOrderAsync(HttpClient client)
    {
        PublishedProduct product = await PublishProductAsync(client, 15m, stock: 2);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, product.Id, 1);
        OrderGroupResponse group = await CheckoutAsync(client);
        return new PlacedSellerOrder(product, customer.AccessToken, group.Id, group.Orders[0]);
    }

    private async Task<PlacedMultiSellerOrder> PlaceMultiSellerOrderAsync(HttpClient client)
    {
        PublishedProduct first = await PublishProductAsync(client, 15m, stock: 2);
        PublishedProduct second = await PublishProductAsync(client, 20m, stock: 2);
        AuthenticationResponse customer = await RegisterAsync(client);
        Authorize(client, customer.AccessToken);
        await AddAsync(client, first.Id, 1);
        await AddAsync(client, second.Id, 1);
        OrderGroupResponse group = await CheckoutAsync(client);
        return new PlacedMultiSellerOrder(
            first,
            second,
            customer.AccessToken,
            group.Id,
            group.Orders.Single(o => o.SellerId == first.SellerId),
            group.Orders.Single(o => o.SellerId == second.SellerId));
    }

    private static async Task<PagedResult<NotificationResponse>> GetInboxAsync(HttpClient client)
    {
        return (await (await client.GetAsync("/api/v1/notifications")).Content
            .ReadFromJsonAsync<PagedResult<NotificationResponse>>(JsonOptions))!;
    }

    private static async Task<OrderResponse> TransitionAsync(HttpClient client, Guid orderId, string action)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/v1/seller/orders/{orderId}/{action}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
    }

    private static async Task<OrderResponse> GetSellerOrderAsync(HttpClient client, Guid orderId)
    {
        return (await (await client.GetAsync($"/api/v1/seller/orders/{orderId}")).Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
    }

    private static async Task<CartResponse> GetCartAsync(HttpClient client)
    {
        return (await (await client.GetAsync("/api/v1/cart")).Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions))!;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private async Task<PublishedProduct> PublishProductAsync(
        HttpClient client,
        decimal price,
        int stock,
        bool withVariant = false)
    {
        (_, string adminToken) = await CreateAdminAsync(client);
        Guid categoryId = await FirstCategoryIdAsync(client);
        AuthenticationResponse sellerUser = await RegisterAsync(client);
        string sellerToken = await ApproveSellerAsync(client, sellerUser, adminToken);
        Authorize(client, sellerToken);
        ProductResponse product = (await (await client.PostAsJsonAsync(
            "/api/v1/seller/products",
            new CreateProductRequest(
                "Order Test Bracelet",
                "Handmade leather bracelet with a brass clasp.",
                categoryId,
                price,
                "EGP",
                null,
                withVariant ? 0 : stock))).Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/images",
                new AddProductImageRequest("products/order.jpg", "https://cdn.local/order.jpg", 1, true))).StatusCode);

        Guid? variantId = null;
        if (withVariant)
        {
            ProductVariantResponse variant = (await (await client.PostAsJsonAsync(
                $"/api/v1/seller/products/{product.Id}/variants",
                new CreateProductVariantRequest(
                    "Small",
                    "ORD-" + Guid.NewGuid().ToString("N")[..8],
                    price,
                    "EGP",
                    stock))).Content.ReadFromJsonAsync<ProductVariantResponse>(JsonOptions))!;
            variantId = variant.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/seller/products/{product.Id}/submit", null)).StatusCode);
        Authorize(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/admin/products/{product.Id}/approve", null)).StatusCode);
        return new PublishedProduct(
            product.Id,
            product.Name,
            product.SellerId,
            categoryId,
            variantId,
            sellerToken,
            adminToken);
    }

    private static async Task<CartResponse> AddAsync(
        HttpClient client,
        Guid productId,
        int quantity,
        Guid? variantId = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new AddCartItemRequest(productId, variantId, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>(JsonOptions))!;
    }

    private static async Task<Guid> FirstCategoryIdAsync(HttpClient client)
    {
        List<CategoryTreeResponse> tree = (await (await client.GetAsync("/api/v1/catalog/categories")).Content
            .ReadFromJsonAsync<List<CategoryTreeResponse>>(JsonOptions))!;
        Assert.NotEmpty(tree);
        return tree[0].Id;
    }

    private async Task<string> ApproveSellerAsync(
        HttpClient client,
        AuthenticationResponse applicant,
        string adminToken)
    {
        Authorize(client, applicant.AccessToken);
        HttpResponseMessage submit = await client.PostAsJsonAsync(
            "/api/v1/seller/applications",
            new SubmitSellerApplicationRequest(
                "Studio " + Guid.NewGuid().ToString("N")[..8],
                "Handmade accessories and crafts studio.",
                "+201000000001"));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        SellerApplicationResponse application =
            (await submit.Content.ReadFromJsonAsync<SellerApplicationResponse>(JsonOptions))!;

        Authorize(client, adminToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/admin/seller-applications/{application.Id}/approve", null)).StatusCode);
        return applicant.AccessToken;
    }

    private async Task<(AuthenticationResponse User, string AccessToken)> CreateAdminAsync(HttpClient client)
    {
        AuthenticationResponse registered = await RegisterAsync(client);
        await _factory.AssignRoleAsync(registered.User.Id, RoleNames.Admin);
        AuthenticationResponse admin = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(registered.User.Email, "StrongPass1!"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
        return (admin, admin.AccessToken);
    }

    private static async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        string email = $"order_{Guid.NewGuid():N}@example.com";
        return (await (await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass1!", "Abdo", "Hendawy"))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;
    }

    private static void Authorize(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private sealed record PlacedSellerOrder(
        PublishedProduct Product,
        string CustomerToken,
        Guid GroupId,
        OrderResponse Order);

    private sealed record PlacedMultiSellerOrder(
        PublishedProduct First,
        PublishedProduct Second,
        string CustomerToken,
        Guid GroupId,
        OrderResponse FirstOrder,
        OrderResponse SecondOrder);

    private sealed record PublishedProduct(
        Guid Id,
        string Name,
        Guid SellerId,
        Guid CategoryId,
        Guid? VariantId,
        string SellerToken,
        string AdminToken);
}
