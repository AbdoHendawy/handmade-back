using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.Items);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable(
            "cart_items",
            table =>
            {
                table.HasCheckConstraint("ck_cart_items_quantity_positive", "quantity > 0");
                table.HasCheckConstraint(
                    "ck_cart_items_quantity_max",
                    $"quantity <= {CartLimits.MaxQuantityPerItem}");
            });
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.PriceSnapshot)
            .HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale)
            .IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.CartId, x.ProductId })
            .IsUnique()
            .HasFilter("variant_id IS NULL")
            .HasDatabaseName("ix_cart_items_one_product_without_variant");

        builder.HasIndex(x => new { x.CartId, x.ProductId, x.VariantId })
            .IsUnique()
            .HasFilter("variant_id IS NOT NULL")
            .HasDatabaseName("ix_cart_items_one_product_variant");

        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(x => x.CartId)
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
    }
}
