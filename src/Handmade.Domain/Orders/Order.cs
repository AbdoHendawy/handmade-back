using Handmade.Domain.Catalog;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders.Events;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Domain.Orders;

public sealed class Order : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    private Order(
        Guid id,
        Guid orderGroupId,
        Guid customerId,
        Guid sellerId,
        string sellerNameSnapshot,
        string customerFirstName,
        string customerLastName,
        string customerEmail,
        OrderDeliverySnapshot delivery,
        string currency)
        : base(id)
    {
        OrderGroupId = orderGroupId;
        CustomerId = customerId;
        SellerId = sellerId;
        SellerNameSnapshot = sellerNameSnapshot;
        CustomerFirstName = customerFirstName;
        CustomerLastName = customerLastName;
        CustomerEmail = customerEmail;
        ApplyDelivery(delivery);
        Status = OrderStatus.Placed;
        Currency = currency;
        Subtotal = 0m;
        Total = 0m;
    }

    public Guid OrderGroupId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid SellerId { get; private set; }

    public string SellerNameSnapshot { get; private set; } = string.Empty;

    public long Number { get; private set; }

    public OrderStatus Status { get; private set; }

    public string Currency { get; private set; } = CatalogMoney.DefaultCurrency;

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    public string CustomerFirstName { get; private set; } = string.Empty;

    public string CustomerLastName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Governorate { get; private set; } = string.Empty;

    public string? PostalCode { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order Create(
        Guid orderGroupId,
        Guid customerId,
        Guid sellerId,
        string sellerNameSnapshot,
        string customerFirstName,
        string customerLastName,
        string customerEmail,
        OrderDeliverySnapshot delivery,
        string currency,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (orderGroupId == Guid.Empty)
        {
            throw new DomainException("Order group is required.") { Code = OrderErrorCodes.OrderNotFound };
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer is required.") { Code = "invalid_customer" };
        }

        if (sellerId == Guid.Empty)
        {
            throw new DomainException("Seller is required.") { Code = OrderErrorCodes.SellerMismatch };
        }

        Order order = new(
            CreateId(),
            orderGroupId,
            customerId,
            sellerId,
            RequireName(sellerNameSnapshot, "Seller name snapshot is required.", "invalid_snapshot"),
            RequireName(customerFirstName, "Customer first name is required.", "invalid_customer"),
            RequireName(customerLastName, "Customer last name is required.", "invalid_customer"),
            RequireEmail(customerEmail),
            delivery,
            CatalogMoney.RequireCurrency(currency));

        order.Raise(new OrderPlaced(order.Id, orderGroupId, sellerId, customerId, now));
        return order;
    }

    public OrderItem AddItem(
        Guid productId,
        Guid? variantId,
        Guid sellerId,
        string productNameSnapshot,
        string? variantNameSnapshot,
        string? skuSnapshot,
        string? imageUrlSnapshot,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        if (sellerId != SellerId)
        {
            throw new DomainException("An order cannot contain items from more than one seller.")
            {
                Code = OrderErrorCodes.SellerMismatch
            };
        }

        string normalizedCurrency = CatalogMoney.RequireCurrency(currency);
        if (!string.Equals(normalizedCurrency, Currency, StringComparison.Ordinal))
        {
            throw new DomainException("Item currency does not match the order currency.")
            {
                Code = OrderErrorCodes.CurrencyMismatch
            };
        }

        OrderItem item = OrderItem.Create(
            Id,
            productId,
            variantId,
            sellerId,
            productNameSnapshot,
            variantNameSnapshot,
            skuSnapshot,
            imageUrlSnapshot,
            quantity,
            unitPrice,
            normalizedCurrency);

        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void RestoreItems(IEnumerable<OrderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items.Clear();
        foreach (OrderItem item in items)
        {
            if (item.OrderId != Id)
            {
                throw new DomainException("Order item does not belong to this order.")
                {
                    Code = OrderErrorCodes.OrderNotFound
                };
            }

            if (item.SellerId != SellerId)
            {
                throw new DomainException("An order cannot contain items from more than one seller.")
                {
                    Code = OrderErrorCodes.SellerMismatch
                };
            }

            if (!string.Equals(item.Currency, Currency, StringComparison.Ordinal))
            {
                throw new DomainException("Item currency does not match the order currency.")
                {
                    Code = OrderErrorCodes.CurrencyMismatch
                };
            }

            _items.Add(item);
        }

        RecalculateTotals();
    }

    public void ApplyTotals(decimal subtotal, decimal total)
    {
        decimal normalizedSubtotal = CatalogMoney.RequireAmount(subtotal);
        decimal normalizedTotal = CatalogMoney.RequireAmount(total);
        decimal expectedSubtotal = SumLineTotals();

        if (normalizedSubtotal != expectedSubtotal)
        {
            throw new DomainException("Order subtotal must equal the sum of line totals.")
            {
                Code = OrderErrorCodes.InvalidPrice
            };
        }

        if (normalizedTotal != normalizedSubtotal)
        {
            throw new DomainException("Order total must equal the order subtotal.")
            {
                Code = OrderErrorCodes.InvalidPrice
            };
        }

        Subtotal = normalizedSubtotal;
        Total = normalizedTotal;
    }

    public void Confirm(DateTimeOffset now)
    {
        Transition(OrderStatus.Placed, OrderStatus.Confirmed);
        Raise(new OrderConfirmed(Id, OrderGroupId, SellerId, CustomerId, now));
    }

    public void Prepare(DateTimeOffset now)
    {
        Transition(OrderStatus.Confirmed, OrderStatus.Preparing);
        Raise(new OrderPreparing(Id, OrderGroupId, SellerId, CustomerId, now));
    }

    public void Ship(DateTimeOffset now)
    {
        Transition(OrderStatus.Preparing, OrderStatus.Shipped);
        Raise(new OrderShipped(Id, OrderGroupId, SellerId, CustomerId, now));
    }

    public void Deliver(DateTimeOffset now)
    {
        Transition(OrderStatus.Shipped, OrderStatus.Delivered);
        Raise(new OrderDelivered(Id, OrderGroupId, SellerId, CustomerId, now));
    }

    public void Cancel(DateTimeOffset now)
    {
        Transition(OrderStatus.Placed, OrderStatus.Cancelled);
        Raise(new OrderCancelled(Id, OrderGroupId, SellerId, CustomerId, now));
    }

    private void Transition(OrderStatus expected, OrderStatus next)
    {
        if (Status != expected)
        {
            throw new ConflictException($"Order cannot move from {Status} to {next}.")
            {
                Code = OrderErrorCodes.InvalidStatusTransition
            };
        }

        Status = next;
    }

    private void RecalculateTotals()
    {
        decimal subtotal = SumLineTotals();
        Subtotal = subtotal;
        Total = subtotal;
    }

    private decimal SumLineTotals()
    {
        decimal subtotal = 0m;
        foreach (OrderItem item in _items)
        {
            subtotal += item.LineTotal;
        }

        return subtotal;
    }

    private void ApplyDelivery(OrderDeliverySnapshot delivery)
    {
        RecipientName = delivery.RecipientName;
        Phone = delivery.Phone;
        AddressLine1 = delivery.AddressLine1;
        AddressLine2 = delivery.AddressLine2;
        City = delivery.City;
        Governorate = delivery.Governorate;
        PostalCode = delivery.PostalCode;
        Notes = delivery.Notes;
    }

    private static string RequireName(string? value, string message, string code)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 || trimmed.Length > NameMaxLength)
        {
            throw new DomainException(message) { Code = code };
        }

        return trimmed;
    }

    private static string RequireEmail(string? email)
    {
        string trimmed = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (trimmed.Length is < 1 || trimmed.Length > EmailMaxLength)
        {
            throw new DomainException("Customer email is required.") { Code = "invalid_customer" };
        }

        return trimmed;
    }
}
