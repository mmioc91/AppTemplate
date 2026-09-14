using System.Reflection;

namespace AppTemplate.SharedKernel;

/// <summary>
/// Base class for DDD-style enumerations that carry behaviour and avoid primitive obsession.
/// Use instead of plain C# enums when you need domain methods or richer comparisons.
/// </summary>
public abstract class Enumeration<TEnum>(int value, string name) : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Dictionary<int, TEnum> Enumerations = CreateEnumerations();

    public int Value { get; } = value;
    public string Name { get; } = name;

    public static TEnum? FromValue(int value) =>
        Enumerations.GetValueOrDefault(value);

    public static TEnum? FromName(string name) =>
        Enumerations.Values.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public bool Equals(Enumeration<TEnum>? other) =>
        other is not null && GetType() == other.GetType() && Value == other.Value;

    public override bool Equals(object? obj) => obj is Enumeration<TEnum> other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Name;

    private static Dictionary<int, TEnum> CreateEnumerations() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => (TEnum)f.GetValue(null)!)
            .ToDictionary(e => e.Value);
}
