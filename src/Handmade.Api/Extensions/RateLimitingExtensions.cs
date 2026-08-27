using System.Threading.RateLimiting;
using Handmade.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Handmade.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string CatalogPolicy = "catalog";

    public static IServiceCollection AddHandmadeRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        RateLimitingOptions rateLimiting = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();
        rateLimiting.EnsureValidForDeployment(environment);
        rateLimiting.ApplyDevelopmentDefaults(environment);

        bool applyLimits = rateLimiting.Enabled;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRateLimitProblemAsync;

            options.AddPolicy(AuthPolicy, httpContext =>
                CreatePartition(httpContext, applyLimits, rateLimiting.Auth));

            options.AddPolicy(CatalogPolicy, httpContext =>
                CreatePartition(httpContext, applyLimits, rateLimiting.Catalog));
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext httpContext,
        bool applyLimits,
        RateLimitWindowOptions window)
    {
        if (!applyLimits)
        {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        // Per-client IP only; do not trust X-Forwarded-For without explicit trusted-proxy configuration.
        return RateLimitPartition.GetFixedWindowLimiter(
            GetPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = TimeSpan.FromSeconds(window.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
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
}
