using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Handmade.Application.Abstractions.Jobs;
using Handmade.Application.Abstractions.Notifications;
using Handmade.Application.Common;
using Handmade.Infrastructure.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handmade.Infrastructure.Jobs;

public static class HangfireServiceCollectionExtensions
{
    public static IServiceCollection AddHandmadeJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IRealtimeNotificationSender, NoOpRealtimeNotificationSender>();

        bool enabled = configuration.GetValue("Hangfire:Enabled", true);
        if (!enabled)
        {
            services.AddSingleton<IBackgroundJobQueue, ImmediateBackgroundJobQueue>();
            return services;
        }

        string connectionString = configuration.GetConnectionString(ApplicationConstants.DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ApplicationConstants.DefaultConnectionStringName}' is required for Hangfire.");

        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseFilter(new AutomaticRetryAttribute { Attempts = 5 })
                .UsePostgreSqlStorage(
                    configure => configure.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        PrepareSchemaIfNecessary = true
                    });
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
            options.Queues = ["default"];
        });

        services.AddSingleton<IBackgroundJobQueue, HangfireBackgroundJobQueue>();
        return services;
    }

    public static IApplicationBuilder UseHandmadeHangfireDashboard(
        this IApplicationBuilder app,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return app;
        }

        if (app.ApplicationServices.GetService<JobStorage>() is null)
        {
            return app;
        }

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new DevelopmentDashboardAuthorizationFilter()],
            DashboardTitle = "Handmade Jobs"
        });

        return app;
    }
}

internal sealed class DevelopmentDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
