namespace Handmade.Domain.Common;

/// <summary>
/// Base type for domain entities identified by a UUIDv7 <see cref="Guid"/>.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; protected set; }

    /// <summary>
    /// Creates a time-ordered identifier suitable for PostgreSQL primary keys.
    /// </summary>
    protected static Guid CreateId() => Guid.CreateVersion7();

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return Id != Guid.Empty && Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
