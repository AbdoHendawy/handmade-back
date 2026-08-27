using System.Text.Json;
using Handmade.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handmade.Api.Tests;

public sealed class GlobalExceptionHandlerObservabilityTests
{
    private const string SentinelMessage = "Password=SENTINEL_TEST";

    [Fact]
    public async Task Production_UnhandledException_ReturnsSafeProblemDetailsWithTraceId()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = new(NullLogger<GlobalExceptionHandler>.Instance, new FakeHostEnvironment(Environments.Production));

        bool handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException(SentinelMessage),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        using JsonDocument doc = await ParseResponseAsync(httpContext);
        Assert.Equal("An unexpected error occurred.", doc.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(SentinelMessage, doc.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_UnhandledException_MayIncludeExceptionMessage()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = new(NullLogger<GlobalExceptionHandler>.Instance, new FakeHostEnvironment(Environments.Development));

        bool handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException(SentinelMessage),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        using JsonDocument doc = await ParseResponseAsync(httpContext);
        Assert.Equal(SentinelMessage, doc.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("traceId").GetString()));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/v1/observability-test";
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<JsonDocument> ParseResponseAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(httpContext.Response.Body);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Handmade.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
