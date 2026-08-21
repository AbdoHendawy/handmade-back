namespace Handmade.Domain.Common;

/// <summary>
/// Opt-in audit timestamps. Applied per entity via EF configuration and interceptor.
/// Soft delete is intentionally not part of this contract.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    DateTimeOffset UpdatedAt { get; }
}
