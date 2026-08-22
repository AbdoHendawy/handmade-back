using Handmade.Application.Seller;
using Handmade.Domain.Identity;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class SellerApplicationConfiguration : IEntityTypeConfiguration<SellerApplication>
{
    public void Configure(EntityTypeBuilder<SellerApplication> builder)
    {
        builder.ToTable("seller_applications");
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.BusinessName)
            .HasMaxLength(SellerLimits.BusinessNameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(SellerLimits.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasMaxLength(SellerLimits.PhoneMaxLength)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(SellerLimits.ReasonMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_seller_applications_one_pending_per_user");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.ToTable("seller_profiles");
        builder.HasKey(x => x.Id);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(x => x.BusinessName)
            .HasMaxLength(SellerLimits.BusinessNameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(SellerLimits.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasMaxLength(SellerLimits.PhoneMaxLength)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SuspensionReason)
            .HasMaxLength(SellerLimits.ReasonMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.ApprovedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Status);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SuspendedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<SellerApplication>()
            .WithMany()
            .HasForeignKey(x => x.SourceApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsActive);
    }
}
