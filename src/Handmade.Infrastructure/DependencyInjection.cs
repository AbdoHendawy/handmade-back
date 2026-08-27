using Handmade.Application.Abstractions.Authentication;
using Handmade.Application.Abstractions.Email;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Security;
using Handmade.Application.Abstractions.Storage;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Common;
using Handmade.Application.Identity.Services;
using Handmade.Infrastructure.Identity.Authentication;
using Handmade.Infrastructure.Identity.Email;
using Handmade.Infrastructure.Identity.Security;
using Handmade.Infrastructure.Jobs;
using Handmade.Infrastructure.Persistence;
using Handmade.Infrastructure.Persistence.Interceptors;
using Handmade.Infrastructure.Persistence.Seeding;
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
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        ValidateJwtSettings(configuration);

        string connectionString = configuration.GetConnectionString(ApplicationConstants.DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ApplicationConstants.DefaultConnectionStringName}' is not configured. Set ConnectionStrings__Default or ConnectionStrings:Default.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableInterceptor>();
        AddFileStorage(services, configuration);
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        services.AddSingleton<IExternalAuthProvider, GoogleIdTokenValidator>();

        services.AddDbContext<HandmadeDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(HandmadeDbContext).Assembly.FullName));

            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<HandmadeDbContext>());
        services.AddHandmadeJobs(configuration);

        return services;
    }

    private static void ValidateJwtSettings(IConfiguration configuration)
    {
        JwtSettings jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwt.SecretKey) ||
            jwt.SecretKey.Length < ApplicationConstants.JwtMinSecretLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SecretKey must be configured and at least {ApplicationConstants.JwtMinSecretLength} characters. Use user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
        }
    }

    private static void AddFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        FileStorageOptions storage = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
            ?? new FileStorageOptions();
        storage.EnsureValidWhenEnabled();

        if (storage.IsMinio)
        {
            services.AddSingleton<IFileStorage, MinioFileStorage>();
            return;
        }

        services.AddSingleton<IFileStorage, NotConfiguredFileStorage>();
    }
}
