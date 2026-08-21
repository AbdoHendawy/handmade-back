namespace Handmade.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations. Mapped to ProblemDetails by the API layer.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? Code { get; init; }
}
