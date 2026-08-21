namespace Handmade.Application.Abstractions.Time;

/// <summary>
/// Abstraction over the system clock for testable timestamps and time-based rules.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
