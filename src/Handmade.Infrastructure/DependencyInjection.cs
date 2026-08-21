using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Storage;
using Handmade.Application.Abstractions.Time;
using Handmade.Infrastructure.Persistence;
using Handmade.Infrastructure.Persistence.Interceptors;
using Handmade.Infrastructure.Services;
using Handmade.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured. Set ConnectionStrings__Default or ConnectionStrings:Default.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableInterceptor>();
        services.AddSingleton<IFileStorage, NotConfiguredFileStorage>();

        services.AddDbContext<HandmadeDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(HandmadeDbContext).Assembly.FullName));

            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<HandmadeDbContext>());

        return services;
    }
}
