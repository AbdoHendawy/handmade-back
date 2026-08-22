using Handmade.Domain.Identity;
using Handmade.Domain.Seller;
using Handmade.Domain.Notifications;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
