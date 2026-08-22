using Handmade.Domain.Catalog;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.DomainEvents);

        builder.Property(x => x.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(Category.SlugMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.ParentCategoryId);
        builder.HasIndex(x => x.IsActive);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "products",
            table => table.HasCheckConstraint("ck_products_stock_quantity_non_negative", "stock_quantity >= 0"));
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsPublic);
        builder.Ignore(x => x.CanDelete);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(Product.SlugMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Product.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Price).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.StockQuantity).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.RejectionReason).HasMaxLength(Product.RejectionReasonMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.SellerId);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Status, x.PublishedAt });

        builder.Ignore(x => x.Images);
        builder.Ignore(x => x.Variants);

        builder.HasOne<SellerProfile>()
            .WithMany()
            .HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageKey).HasMaxLength(ProductImage.StorageKeyMaxLength).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(ProductImage.UrlMaxLength).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsPrimary).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_images_product_id");
        builder.HasIndex(x => x.ProductId)
            .IsUnique()
            .HasFilter("is_primary = TRUE")
            .HasDatabaseName("ix_product_images_one_primary_per_product");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable(
            "product_variants",
            table => table.HasCheckConstraint(
                "ck_product_variants_stock_quantity_non_negative",
                "stock_quantity >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.Name).HasMaxLength(ProductVariant.NameMaxLength).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(ProductVariant.SkuMaxLength).IsRequired();
        builder.Property(x => x.Price).HasPrecision(CatalogMoney.Precision, CatalogMoney.Scale).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.StockQuantity).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.ProductId);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
