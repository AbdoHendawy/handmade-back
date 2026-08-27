using Handmade.Application.Common;
using Handmade.Infrastructure.Identity.Email;
using Handmade.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Configuration;

/// <summary>
/// Staging/Production startup fail-safes so deployment never silently uses local Development defaults.
/// </summary>
public static class DeploymentConfigurationGuard
{
    public const string KnownDevelopmentPasswordMarker = "Password=handmade";

    private static readonly string[] LocalHosts =
    [
        "localhost",
        "127.0.0.1",
        "::1"
    ];

    public static bool RequiresStrictConfig(IHostEnvironment environment) =>
        environment.IsProduction() || environment.IsStaging();

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!RequiresStrictConfig(environment))
        {
            return;
        }

        ValidateConnectionString(configuration);
        ValidateAllowedHosts(configuration);
        ValidateCors(configuration);
        ValidateEmail(configuration);
        ValidateFileStorage(configuration);
    }

    private static void ValidateConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ApplicationConstants.DefaultConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ApplicationConstants.DefaultConnectionStringName}' is required in Staging/Production. Set ConnectionStrings__Default via environment or secrets.");
        }

        if (ContainsKnownDevelopmentPassword(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default must not use the repository development database password in Staging/Production.");
        }

        string? host = TryGetConnectionHost(connectionString);
        if (host is not null && IsLocalHost(host))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default must not point to localhost/127.0.0.1/::1 in Staging/Production.");
        }
    }

    private static void ValidateAllowedHosts(IConfiguration configuration)
    {
        string? allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*")
        {
            throw new InvalidOperationException(
                "AllowedHosts must be configured to specific host name(s) in Staging/Production (not '*'). Set AllowedHosts via environment variables.");
        }
    }

    private static void ValidateCors(IConfiguration configuration)
    {
        CorsOptions cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        if (cors.AllowedOrigins.Length == 0
            || cors.AllowedOrigins.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must be configured explicitly in Staging/Production. Set Cors__AllowedOrigins__0 (and additional indices) via environment variables.");
        }
    }

    private static void ValidateEmail(IConfiguration configuration)
    {
        EmailOptions email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (!email.IsSmtp)
        {
            throw new InvalidOperationException(
                "Email:Provider must be SMTP in Staging/Production. Console email is Development-only.");
        }

        email.EnsureValidWhenSmtp();
    }

    private static void ValidateFileStorage(IConfiguration configuration)
    {
        FileStorageOptions storage = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
            ?? new FileStorageOptions();

        if (!storage.IsMinio)
        {
            throw new InvalidOperationException(
                "FileStorage:Provider must be MinIO in Staging/Production. Configure FileStorage via environment variables.");
        }

        storage.EnsureValidWhenEnabled();
    }

    private static bool ContainsKnownDevelopmentPassword(string connectionString) =>
        connectionString.Contains(KnownDevelopmentPasswordMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalHost(string host)
    {
        string trimmed = host.Trim().Trim('[', ']');
        return LocalHosts.Any(local => string.Equals(trimmed, local, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryGetConnectionHost(string connectionString)
    {
        foreach (string segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = segment.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = segment[..separator].Trim();
            string value = segment[(separator + 1)..].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
