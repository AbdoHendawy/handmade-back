using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<ExternalLogin> ExternalLogins { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<SellerApplication> SellerApplications { get; }

    DbSet<SellerProfile> SellerProfiles { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductImage> ProductImages { get; }

    DbSet<ProductVariant> ProductVariants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
