namespace AppTemplate.SharedKernel;

/// <summary>
/// Base class for value objects. Value objects are immutable objects defined
/// by their attributes rather than identity. Two value objects with the same
/// attribute values are considered equal.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is ValueObject other && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public bool Equals(ValueObject? other) => Equals((object?)other);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj.GetHashCode()));

    public static bool operator ==(ValueObject? left, ValueObject? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
