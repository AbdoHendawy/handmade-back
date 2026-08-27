using System.Diagnostics;

namespace Handmade.Api.Configuration;

/// <summary>
/// Shared request correlation identifier used by logging scopes and ProblemDetails.
/// </summary>
public static class RequestDiagnostics
{
    public static string GetTraceId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Activity.Current?.Id ?? context.TraceIdentifier;
    }
}
