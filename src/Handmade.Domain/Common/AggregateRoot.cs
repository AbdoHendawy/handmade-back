namespace Handmade.Domain.Common;

/// <summary>
/// Marker base for aggregate roots. Domain events can be added here later without an event bus.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }
}
