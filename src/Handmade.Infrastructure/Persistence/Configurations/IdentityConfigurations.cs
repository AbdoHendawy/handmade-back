using Handmade.Application.Identity;
using Handmade.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handmade.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasMaxLength(IdentityLimits.EmailMaxLength)
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(IdentityLimits.PasswordHashMaxLength);

        builder.Property(x => x.FirstName)
            .HasMaxLength(IdentityLimits.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(IdentityLimits.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsEmailVerified).IsRequired();
        builder.Property(x => x.WelcomeEmailSent).IsRequired();
        builder.Property(x => x.SecurityStamp).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ExternalLogins)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.UserRoles).HasField("_userRoles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.ExternalLogins).HasField("_externalLogins").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.RefreshTokens).HasField("_refreshTokens").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(IdentityLimits.RoleNameMaxLength).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(x => new { x.UserId, x.RoleId });

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RoleId);
    }
}

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(IdentityLimits.ProviderMaxLength).IsRequired();
        builder.Property(x => x.ProviderUserId).HasMaxLength(IdentityLimits.ProviderUserIdMaxLength).IsRequired();
        builder.Property(x => x.ProviderEmail).HasMaxLength(IdentityLimits.EmailMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).HasMaxLength(IdentityLimits.RefreshTokenHashMaxLength).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(IdentityLimits.IpAddressMaxLength);
        builder.Property(x => x.RevokedByIp).HasMaxLength(IdentityLimits.IpAddressMaxLength);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
