namespace Handmade.Domain.Exceptions;

/// <summary>
/// Raised when the caller is authenticated but not allowed to perform the operation.
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message)
        : base(message)
    {
        Code = "forbidden";
    }
}
