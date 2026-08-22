using Handmade.Application.Notifications;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.CanDeliver);

        builder.Property(x => x.Type)
            .HasMaxLength(NotificationLimits.TypeMaxLength)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(NotificationLimits.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasMaxLength(NotificationLimits.BodyMaxLength)
            .IsRequired();

        builder.Property(x => x.DataJson)
            .HasMaxLength(NotificationLimits.DataJsonMaxLength);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(NotificationLimits.IdempotencyKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(NotificationLimits.LastErrorMaxLength);

        builder.Property(x => x.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.IsRead).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
