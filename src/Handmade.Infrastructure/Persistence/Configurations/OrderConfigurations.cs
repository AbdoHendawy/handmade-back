using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Orders;
using Handmade.Domain.Orders.ValueObjects;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class OrderGroupConfiguration : IEntityTypeConfiguration<OrderGroup>
{
    public void Configure(EntityTypeBuilder<OrderGroup> builder)
    {
        builder.ToTable(
            "order_groups",
            table =>
            {
                table.HasCheckConstraint("ck_order_groups_subtotal_non_negative", "subtotal >= 0");
                table.HasCheckConstraint("ck_order_groups_total_non_negative", "total >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.Number)
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.Number).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(x => x.Number).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.Total).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.CustomerFirstName).HasMaxLength(OrderGroup.NameMaxLength).IsRequired();
        builder.Property(x => x.CustomerLastName).HasMaxLength(OrderGroup.NameMaxLength).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(OrderGroup.EmailMaxLength).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(OrderDeliverySnapshot.RecipientNameMaxLength).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(OrderDeliverySnapshot.PhoneMaxLength).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(OrderDeliverySnapshot.AddressLineMaxLength).IsRequired();
        builder.Property(x => x.AddressLine2).HasMaxLength(OrderDeliverySnapshot.AddressLineMaxLength);
        builder.Property(x => x.City).HasMaxLength(OrderDeliverySnapshot.CityMaxLength).IsRequired();
        builder.Property(x => x.Governorate).HasMaxLength(OrderDeliverySnapshot.GovernorateMaxLength).IsRequired();
        builder.Property(x => x.PostalCode).HasMaxLength(OrderDeliverySnapshot.PostalCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(OrderDeliverySnapshot.NotesMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(
            "orders",
            table =>
            {
                table.HasCheckConstraint("ck_orders_subtotal_non_negative", "subtotal >= 0");
                table.HasCheckConstraint("ck_orders_total_non_negative", "total >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.Items);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.Number)
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.Number).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(x => x.Number).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.SellerNameSnapshot).HasMaxLength(Order.NameMaxLength).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.Total).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.CustomerFirstName).HasMaxLength(Order.NameMaxLength).IsRequired();
        builder.Property(x => x.CustomerLastName).HasMaxLength(Order.NameMaxLength).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(Order.EmailMaxLength).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(OrderDeliverySnapshot.RecipientNameMaxLength).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(OrderDeliverySnapshot.PhoneMaxLength).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(OrderDeliverySnapshot.AddressLineMaxLength).IsRequired();
        builder.Property(x => x.AddressLine2).HasMaxLength(OrderDeliverySnapshot.AddressLineMaxLength);
        builder.Property(x => x.City).HasMaxLength(OrderDeliverySnapshot.CityMaxLength).IsRequired();
        builder.Property(x => x.Governorate).HasMaxLength(OrderDeliverySnapshot.GovernorateMaxLength).IsRequired();
        builder.Property(x => x.PostalCode).HasMaxLength(OrderDeliverySnapshot.PostalCodeMaxLength);
        builder.Property(x => x.Notes).HasMaxLength(OrderDeliverySnapshot.NotesMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.OrderGroupId);
        builder.HasIndex(x => x.SellerId);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne<OrderGroup>()
            .WithMany()
            .HasForeignKey(x => x.OrderGroupId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<SellerProfile>()
            .WithMany()
            .HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable(
            "order_items",
            table =>
            {
                table.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
                table.HasCheckConstraint("ck_order_items_unit_price_non_negative", "unit_price >= 0");
                table.HasCheckConstraint("ck_order_items_line_total_non_negative", "line_total >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(OrderItem.ProductNameMaxLength).IsRequired();
        builder.Property(x => x.VariantNameSnapshot).HasMaxLength(OrderItem.VariantNameMaxLength);
        builder.Property(x => x.SkuSnapshot).HasMaxLength(OrderItem.SkuMaxLength);
        builder.Property(x => x.ImageUrlSnapshot).HasMaxLength(OrderItem.ImageUrlMaxLength);
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.LineTotal).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.SellerId);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<SellerProfile>()
            .WithMany()
            .HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
