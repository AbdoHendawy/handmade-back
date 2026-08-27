using System.Text.Json.Serialization;
using Handmade.Api.Configuration;
using Handmade.Api.Middleware;
using Handmade.Api.Notifications;
using Handmade.Application;
using Handmade.Application.Abstractions.Notifications;
using Handmade.Application.Common;
using Handmade.Application.Notifications;
using Handmade.Infrastructure;
using Handmade.Infrastructure.Jobs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Handmade.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHandmadeApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        DeploymentConfigurationGuard.Validate(configuration, environment);

        services.AddApplication();
        services.AddInfrastructure(configuration, environment);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
        });

        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 20 * 1024 * 1024;
        });

        services.AddCorsConfiguration(configuration, environment);
        services.AddHandmadeHealthChecks(configuration);
        services.AddHandmadeOpenApi();
        services.AddHandmadeAuthentication(configuration);
        services.AddHandmadeRealtime();
        services.AddHandmadeRateLimiting(configuration);
        services.AddHandmadeObservability();
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
            options.Preload = false;
        });

        return services;
    }

    private static IServiceCollection AddHandmadeRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();
        services.AddSingleton<IRealtimeNotificationSender, SignalRNotificationSender>();
        return services;
    }

    private static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        CorsOptions corsOptions = configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>() ?? new CorsOptions();

        if (corsOptions.AllowedOrigins.Length == 0 && environment.IsDevelopment())
        {
            corsOptions.AllowedOrigins = ["http://localhost:4200"];
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.DefaultPolicyName, policy =>
            {
                if (corsOptions.AllowedOrigins.Length == 0)
                {
                    return;
                }

                policy
                    .WithOrigins(corsOptions.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddHandmadeHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ApplicationConstants.DefaultConnectionStringName);

        IHealthChecksBuilder healthChecks = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            healthChecks.AddNpgSql(
                connectionString,
                name: "postgres",
                tags: ["ready"]);
        }

        return services;
    }

    private static IServiceCollection AddHandmadeOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = ApplicationConstants.ApiName,
                    Version = "v1",
                    Description = "Handmade Art & Crafts Gallery API with Identity, Seller, Notifications, and Catalog."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT access token. Example: eyJhbGciOiJIUzI1NiIs..."
                };

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                });

                return Task.CompletedTask;
            });
        });

        return services;
    }
}

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseHandmadePipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHandmadeObservability();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCors(CorsOptions.DefaultPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHandmadeHangfireDashboard(app.Environment);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", $"{ApplicationConstants.ApiName} v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = ApplicationConstants.ApiName;
                options.DisplayRequestDuration();
                options.EnablePersistAuthorization();
                options.EnableTryItOutByDefault();
                options.EnableFilter();
            });
            app.MapScalarApiReference(options =>
            {
                options.WithTitle(ApplicationConstants.ApiName);
            });
        }

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapControllers();
        app.MapHub<NotificationHub>(NotificationHubRoutes.Notifications);

        return app;
    }
}
