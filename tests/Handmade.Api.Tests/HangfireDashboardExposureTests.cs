using System.Net;
using Handmade.Infrastructure.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handmade.Api.Tests;

/// <summary>
/// Verifies Hangfire dashboard stays Development-only without booting a full
/// Staging/Production API host (those environments reject localhost Testcontainers DB).
/// </summary>
public sealed class HangfireDashboardExposureTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopment_DoesNotMapHangfireDashboard(string environmentName)
    {
        await using WebApplication app = CreateApp(environmentName);
        await app.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };

        HttpResponseMessage response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Development_WithoutJobStorage_DoesNotMapHangfireDashboard()
    {
        await using WebApplication app = CreateApp(Environments.Development);
        await app.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };

        HttpResponseMessage response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplication CreateApp(string environmentName)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRouting();

        WebApplication app = builder.Build();
        app.UseHandmadeHangfireDashboard(app.Environment);
        app.MapGet("/{**path}", () => Results.NotFound());
        return app;
    }
}
