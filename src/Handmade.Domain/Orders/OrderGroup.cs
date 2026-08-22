using Handmade.Domain.Catalog;
using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Orders.Events;
using Handmade.Domain.Orders.ValueObjects;

namespace Handmade.Domain.Orders;

public sealed class OrderGroup : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;

    private OrderGroup()
    {
    }

    private OrderGroup(
        Guid id,
        Guid customerId,
        string customerFirstName,
        string customerLastName,
        string customerEmail,
        OrderDeliverySnapshot delivery,
        string currency,
        PaymentMethod paymentMethod)
        : base(id)
    {
        CustomerId = customerId;
        CustomerFirstName = customerFirstName;
        CustomerLastName = customerLastName;
        CustomerEmail = customerEmail;
        ApplyDelivery(delivery);
        Status = OrderGroupStatus.Placed;
        PaymentMethod = RequireSupported(paymentMethod);
        Currency = currency;
        Subtotal = 0m;
        Total = 0m;
    }

    public Guid CustomerId { get; private set; }

    public OrderGroupStatus Status { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public long Number { get; private set; }

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

    public static OrderGroup Create(
        Guid customerId,
        string customerFirstName,
        string customerLastName,
        string customerEmail,
        OrderDeliverySnapshot delivery,
        string currency,
        PaymentMethod paymentMethod,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer is required.") { Code = "invalid_customer" };
        }

        OrderGroup group = new(
            CreateId(),
            customerId,
            RequireName(customerFirstName, "Customer first name is required."),
            RequireName(customerLastName, "Customer last name is required."),
            RequireEmail(customerEmail),
            delivery,
            CatalogMoney.RequireCurrency(currency),
            paymentMethod);

        group.Raise(new OrderGroupPlaced(group.Id, customerId, now));
        return group;
    }

    public void ApplyTotals(decimal subtotal, decimal total)
    {
        decimal normalizedSubtotal = CatalogMoney.RequireAmount(subtotal);
        decimal normalizedTotal = CatalogMoney.RequireAmount(total);

        if (normalizedTotal != normalizedSubtotal)
        {
            throw new DomainException("Order group total must equal the order group subtotal.")
            {
                Code = OrderErrorCodes.InvalidPrice
            };
        }

        Subtotal = normalizedSubtotal;
        Total = normalizedTotal;
    }

    public void ApplyTotalsFromOrders(IReadOnlyList<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        decimal subtotal = 0m;
        decimal total = 0m;
        foreach (Order order in orders)
        {
            if (order.OrderGroupId != Id)
            {
                throw new DomainException("Order does not belong to this order group.")
                {
                    Code = OrderErrorCodes.OrderNotFound
                };
            }

            if (order.CustomerId != CustomerId)
            {
                throw new DomainException("Order customer does not match the order group customer.")
                {
                    Code = "invalid_customer"
                };
            }

            if (!string.Equals(order.Currency, Currency, StringComparison.Ordinal))
            {
                throw new DomainException("Order currency does not match the order group currency.")
                {
                    Code = OrderErrorCodes.CurrencyMismatch
                };
            }

            subtotal += order.Subtotal;
            total += order.Total;
        }

        ApplyTotals(subtotal, total);
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

    private static PaymentMethod RequireSupported(PaymentMethod paymentMethod)
    {
        if (!Enum.IsDefined(paymentMethod))
        {
            throw new DomainException("Payment method is not supported.");
        }

        return paymentMethod;
    }

    private static string RequireName(string? value, string message)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 || trimmed.Length > NameMaxLength)
        {
            throw new DomainException(message) { Code = "invalid_customer" };
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
