using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;
using CartEntity = Handmade.Domain.Cart.Cart;

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

    DbSet<CartEntity> Carts { get; }

    DbSet<CartItem> CartItems { get; }

    DbSet<OrderGroup> OrderGroups { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    /// <summary>
    /// Detaches tracked entities so a cart mutation can retry after a unique/concurrency conflict.
    /// </summary>
    void ClearTrackedEntities();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
