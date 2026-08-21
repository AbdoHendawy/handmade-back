namespace Handmade.Domain.Exceptions;

/// <summary>
/// Raised when a requested domain resource does not exist.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message)
    {
        Code = "not_found";
    }

    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} '{key}' was not found.")
    {
        Code = "not_found";
        ResourceName = resourceName;
        Key = key;
    }

    public string? ResourceName { get; }

    public object? Key { get; }
}
