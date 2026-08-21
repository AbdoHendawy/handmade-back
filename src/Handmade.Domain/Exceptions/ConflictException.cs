namespace Handmade.Domain.Exceptions;

/// <summary>
/// Raised when an operation conflicts with the current state of a resource.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message)
    {
        Code = "conflict";
    }
}
