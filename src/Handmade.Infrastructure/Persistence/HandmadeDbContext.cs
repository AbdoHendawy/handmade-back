using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Cart;
using Handmade.Domain.Catalog;
using Handmade.Domain.Identity;
using Handmade.Domain.Notifications;
using Handmade.Domain.Orders;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Infrastructure.Persistence;

public sealed class HandmadeDbContext : DbContext, IApplicationDbContext
{
    public HandmadeDbContext(DbContextOptions<HandmadeDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SellerApplication> SellerApplications => Set<SellerApplication>();

    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<OrderGroup> OrderGroups => Set<OrderGroup>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public void ClearTrackedEntities() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandmadeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
