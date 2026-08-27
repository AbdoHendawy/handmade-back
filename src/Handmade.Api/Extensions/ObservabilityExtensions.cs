using Handmade.Api.Configuration;
using Microsoft.AspNetCore.HttpLogging;

namespace Handmade.Api.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddHandmadeObservability(this IServiceCollection services)
    {
        services.AddHttpLogging(options =>
        {
            options.LoggingFields =
                HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.RequestQuery
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.RequestHeaders.Clear();
            options.ResponseHeaders.Clear();
            options.RequestBodyLogLimit = 0;
            options.ResponseBodyLogLimit = 0;
        });

        return services;
    }

    public static ILoggingBuilder AddHandmadeLogging(
        this ILoggingBuilder logging,
        IHostEnvironment environment)
    {
        logging.ClearProviders();

        if (environment.IsDevelopment())
        {
            logging.AddConsole();
            logging.AddDebug();
        }
        else
        {
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "O";
            });
        }

        return logging;
    }

    public static WebApplication UseHandmadeObservability(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            string traceId = RequestDiagnostics.GetTraceId(context);
            using (app.Logger.BeginScope(new Dictionary<string, object>
            {
                ["traceId"] = traceId
            }))
            {
                await next();
            }
        });

        app.UseHttpLogging();
        return app;
    }
}
