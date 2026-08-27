using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public const int DefaultAuthPermitLimit = 20;

    public const int DefaultAuthWindowSeconds = 60;

    public const int DefaultCatalogPermitLimit = 120;

    public const int DefaultCatalogWindowSeconds = 60;

    public const int MaxPermitLimit = 10_000;

    public const int MaxWindowSeconds = 3_600;

    public bool Enabled { get; set; } = true;

    public RateLimitWindowOptions Auth { get; set; } = new();

    public RateLimitWindowOptions Catalog { get; set; } = new();

    public static bool RequiresStrictConfig(IHostEnvironment environment) =>
        environment.IsProduction() || environment.IsStaging();

    public void EnsureValidForDeployment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!RequiresStrictConfig(environment))
        {
            return;
        }

        if (!Enabled)
        {
            throw new InvalidOperationException(
                "RateLimiting:Enabled must be true in Staging/Production. Disabling rate limiting is not allowed outside Development.");
        }

        ValidateWindow(Auth, "RateLimiting:Auth");
        ValidateWindow(Catalog, "RateLimiting:Catalog");
    }

    public void ApplyDevelopmentDefaults(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (RequiresStrictConfig(environment))
        {
            return;
        }

        Auth.ApplyDefaultsIfMissing(DefaultAuthPermitLimit, DefaultAuthWindowSeconds);
        Catalog.ApplyDefaultsIfMissing(DefaultCatalogPermitLimit, DefaultCatalogWindowSeconds);
    }

    private static void ValidateWindow(RateLimitWindowOptions window, string sectionName)
    {
        if (window.PermitLimit is < 1 or > MaxPermitLimit)
        {
            throw new InvalidOperationException(
                $"{sectionName}:PermitLimit must be between 1 and {MaxPermitLimit} in Staging/Production.");
        }

        if (window.WindowSeconds is < 1 or > MaxWindowSeconds)
        {
            throw new InvalidOperationException(
                $"{sectionName}:WindowSeconds must be between 1 and {MaxWindowSeconds} in Staging/Production.");
        }
    }
}

public sealed class RateLimitWindowOptions
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    internal void ApplyDefaultsIfMissing(int defaultPermitLimit, int defaultWindowSeconds)
    {
        if (PermitLimit <= 0)
        {
            PermitLimit = defaultPermitLimit;
        }

        if (WindowSeconds <= 0)
        {
            WindowSeconds = defaultWindowSeconds;
        }
    }
}
