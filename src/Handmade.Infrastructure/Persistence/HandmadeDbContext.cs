using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Identity;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandmadeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
