using Handmade.Domain.Catalog;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Handmade.Infrastructure.Persistence.Seeding;

public static class CatalogSeed
{
    private static readonly (string Name, string Slug)[] Roots =
    [
        ("Home Decor", "home-decor"),
        ("Jewelry", "jewelry"),
        ("Accessories", "accessories"),
        ("Artwork", "artwork"),
        ("Gifts", "gifts")
    ];

    public static async Task SeedDevelopmentCategoriesAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogSeed");

        foreach ((string name, string slug) in Roots)
        {
            bool exists = await db.Categories.AnyAsync(c => c.Slug == slug, cancellationToken);
            if (exists)
            {
                continue;
            }

            db.Categories.Add(Category.Create(name, slug, null, null, DateTimeOffset.UtcNow));
            logger.LogInformation("Seeding category {Slug}", slug);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
