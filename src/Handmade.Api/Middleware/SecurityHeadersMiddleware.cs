namespace Handmade.Api.Middleware;

/// <summary>
/// Applies a conservative set of security-related response headers.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-XSS-Protection"] = "0";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
