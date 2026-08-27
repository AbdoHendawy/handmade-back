using FluentValidation;
using Handmade.Api.Configuration;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Handmade.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = MapException(httpContext, exception);
        string traceId = RequestDiagnostics.GetTraceId(httpContext);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} with {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Handled exception {StatusCode} for {Method} {Path} with {TraceId}",
                problem.Status,
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private ProblemDetails MapException(HttpContext httpContext, Exception exception)
    {
        string traceId = RequestDiagnostics.GetTraceId(httpContext);

        ProblemDetails problem = exception switch
        {
            ValidationException validationException => CreateValidationProblem(validationException),
            NotFoundException notFound => CreateProblem(
                StatusCodes.Status404NotFound,
                "Not Found",
                notFound.Message,
                notFound.Code),
            ConflictException conflict => CreateProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                conflict.Message,
                conflict.Code),
            ForbiddenException forbidden => CreateProblem(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                forbidden.Message,
                forbidden.Code),
            DomainException domainException => CreateProblem(
                StatusCodes.Status400BadRequest,
                "Domain Rule Violation",
                domainException.Message,
                domainException.Code),
            UnauthorizedAccessException unauthorized => CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                string.IsNullOrWhiteSpace(unauthorized.Message)
                    ? "Authentication is required."
                    : unauthorized.Message,
                "unauthorized"),
            KeyNotFoundException keyNotFound => CreateProblem(
                StatusCodes.Status404NotFound,
                "Not Found",
                keyNotFound.Message,
                "not_found"),
            DbUpdateConcurrencyException => CreateProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another operation.",
                SellerErrorCodes.ConcurrencyConflict),
            DbUpdateException dbUpdate when IsUniqueViolation(dbUpdate) => CreateProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request conflicts with an existing resource.",
                "conflict"),
            _ => CreateProblem(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred.",
                "internal_error")
        };

        problem.Extensions["traceId"] = traceId;
        problem.Instance = httpContext.Request.Path;
        return problem;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgres
               && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    private static ProblemDetails CreateValidationProblem(ValidationException exception)
    {
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Detail = "One or more validation errors occurred.",
            Extensions =
            {
                ["errors"] = errors,
                ["code"] = "validation_failed"
            }
        };
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string? code)
    {
        ProblemDetails problem = new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = status switch
            {
                StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc6585#section-4",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }
}
