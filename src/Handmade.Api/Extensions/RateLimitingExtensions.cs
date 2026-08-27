using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Handmade.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string CatalogPolicy = "catalog";

    public static IServiceCollection AddHandmadeRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        bool enabled = configuration.GetValue("RateLimiting:Enabled", true);
        RateLimitWindowOptions auth = BindWindow(configuration, "RateLimiting:Auth", permitLimit: 20, windowSeconds: 60);
        RateLimitWindowOptions catalog = BindWindow(configuration, "RateLimiting:Catalog", permitLimit: 120, windowSeconds: 60);

        if (!enabled)
        {
            auth = new RateLimitWindowOptions { PermitLimit = int.MaxValue, WindowSeconds = 60 };
            catalog = new RateLimitWindowOptions { PermitLimit = int.MaxValue, WindowSeconds = 60 };
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRateLimitProblemAsync;

            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = auth.PermitLimit,
                        Window = TimeSpan.FromSeconds(auth.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(CatalogPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = catalog.PermitLimit,
                        Window = TimeSpan.FromSeconds(catalog.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static RateLimitWindowOptions BindWindow(
        IConfiguration configuration,
        string sectionName,
        int permitLimit,
        int windowSeconds)
    {
        RateLimitWindowOptions options = configuration.GetSection(sectionName).Get<RateLimitWindowOptions>()
            ?? new RateLimitWindowOptions();

        if (options.PermitLimit <= 0)
        {
            options.PermitLimit = permitLimit;
        }

        if (options.WindowSeconds <= 0)
        {
            options.WindowSeconds = windowSeconds;
        }

        return options;
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static async ValueTask WriteRateLimitProblemAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;
        string traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please try again later.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["code"] = "rate_limited",
                ["traceId"] = traceId
            }
        };

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    }

    private sealed class RateLimitWindowOptions
    {
        public int PermitLimit { get; set; }

        public int WindowSeconds { get; set; }
    }
}
