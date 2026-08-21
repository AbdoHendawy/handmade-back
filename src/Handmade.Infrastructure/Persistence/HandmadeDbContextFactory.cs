using Handmade.Infrastructure.Persistence;
using Handmade.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Handmade.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// </summary>
public sealed class HandmadeDbContextFactory : IDesignTimeDbContextFactory<HandmadeDbContext>
{
    public HandmadeDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            string apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Handmade.Api"));
            if (!Directory.Exists(apiPath))
            {
                apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/Handmade.Api"));
            }

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(apiPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("Default");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=handmade;Username=handmade;Password=handmade";
        }

        DbContextOptionsBuilder<HandmadeDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new HandmadeDbContext(optionsBuilder.Options);
    }
}
