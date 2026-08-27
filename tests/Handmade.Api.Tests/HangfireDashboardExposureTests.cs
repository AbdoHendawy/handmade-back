using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class HangfireDashboardExposureTests
{
    private readonly HandmadeApiFactory _factory;

    public HangfireDashboardExposureTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Production_DoesNotExposeHangfireDashboardAnonymously()
    {
        await using NonDevelopmentHangfireFactory production = new(_factory, "Production");
        using HttpClient client = production.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Staging_DoesNotExposeHangfireDashboardAnonymously()
    {
        await using NonDevelopmentHangfireFactory staging = new(_factory, "Staging");
        using HttpClient client = staging.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class NonDevelopmentHangfireFactory : WebApplicationFactory<Program>
    {
        private readonly HandmadeApiFactory _inner;
        private readonly string _environment;

        public NonDevelopmentHangfireFactory(HandmadeApiFactory inner, string environment)
        {
            _inner = inner;
            _environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _inner.EnsureMigrated();
            builder.UseEnvironment(_environment);
            builder.UseSetting("ConnectionStrings:Default", _inner.PostgresConnectionString);
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
            builder.UseSetting("Jwt:SecretKey", "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG!!");
            builder.UseSetting("Jwt:Issuer", "Handmade");
            builder.UseSetting("Jwt:Audience", "Handmade");
            builder.UseSetting("Hangfire:Enabled", "true");
            builder.UseSetting("AdminSeed:Enabled", "false");
            builder.UseSetting("FileStorage:Provider", string.Empty);
            builder.UseSetting("Email:Provider", "Console");
        }
    }
}
