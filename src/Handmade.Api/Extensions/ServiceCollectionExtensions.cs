using System.Text.Json.Serialization;
using Handmade.Api.Configuration;
using Handmade.Api.Middleware;
using Handmade.Application;
using Handmade.Application.Common;
using Handmade.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
        services.AddApplication();
        services.AddInfrastructure(configuration);

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
                    .AllowAnyMethod();
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
                    Description = "Handmade Art & Crafts Gallery API with Identity & Authentication."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = $"Paste a JWT access token from /{ApiRoutes.Auth}/login or /register."
                };

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
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors(CorsOptions.DefaultPolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
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

        return app;
    }
}
