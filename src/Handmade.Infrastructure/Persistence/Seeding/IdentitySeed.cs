using Handmade.Domain.Identity;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
}
