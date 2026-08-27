using Handmade.Application.Abstractions.Security;
using Handmade.Domain.Identity;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handmade.Infrastructure.Persistence.Seeding;

public static class IdentitySeed
{
    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        foreach (string roleName in RoleNames.All)
        {
            bool exists = await db.Roles.AnyAsync(r => r.Name == roleName, cancellationToken);
            if (!exists)
            {
                db.Roles.Add(Role.Create(roleName));
                logger.LogInformation("Seeding role {RoleName}", roleName);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        AdminSeedOptions options = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        options.EnsureValidWhenEnabled();

        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        string email = User.NormalizeEmail(options.Email);
        Role adminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin, cancellationToken);
        User? existing = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existing is not null)
        {
            if (existing.UserRoles.All(ur => ur.RoleId != adminRole.Id))
            {
                existing.AssignRole(adminRole);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Assigned Admin role to existing user {Email}", email);
            }

            return;
        }

        string passwordHash = hasher.HashPassword(options.Password);
        User admin = User.RegisterLocal(email, passwordHash, "Admin", "User");
        admin.AssignRole(adminRole);
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded Admin user {Email}", email);
    }
}
