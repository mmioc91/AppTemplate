using System.Reflection;

namespace AppTemplate.SharedKernel;

/// <summary>
/// Base class for DDD-style enumerations that carry behaviour and avoid primitive obsession.
/// Use instead of plain C# enums when you need domain methods or richer comparisons.
/// </summary>
/// <remarks>
/// Values and names must be unique, names case-insensitively (as <see cref="FromName"/> matches them): values are
/// stored by name, so a repeated one would read back as another. A duplicate fails the type's first lookup with a
/// <see cref="TypeInitializationException"/>.
/// </remarks>
public abstract class Enumeration<TEnum>(int value, string name) : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly TEnum[] Declared = DiscoverDeclared();
    private static readonly Dictionary<int, TEnum> ByValue = Index(e => e.Value, EqualityComparer<int>.Default, "value");
    private static readonly Dictionary<string, TEnum> ByName = Index(e => e.Name, StringComparer.OrdinalIgnoreCase, "name");

    public int Value { get; } = value;
    public string Name { get; } = name;

    public static TEnum? FromValue(int value) =>
        ByValue.GetValueOrDefault(value);

    public static TEnum? FromName(string? name) =>
        name is null ? null : ByName.GetValueOrDefault(name);

    public static IReadOnlyCollection<TEnum> GetAll() => ByValue.Values;

    public bool Equals(Enumeration<TEnum>? other) =>
        other is not null && GetType() == other.GetType() && Value == other.Value;

    public override bool Equals(object? obj) => obj is Enumeration<TEnum> other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Name;

    private static TEnum[] DiscoverDeclared() =>
        typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => (TEnum)f.GetValue(null)!)
            .ToArray();

    private static Dictionary<TKey, TEnum> Index<TKey>(
        Func<TEnum, TKey> keyOf, IEqualityComparer<TKey> comparer, string keyKind)
        where TKey : notnull
    {
        Dictionary<TKey, TEnum> index = new(comparer);
        foreach (TEnum declared in Declared)
        {
            if (!index.TryAdd(keyOf(declared), declared))
            {
                throw new InvalidOperationException(
                    $"{typeof(TEnum).Name} declares the {keyKind} '{keyOf(declared)}' more than once.");
            }
        }

        return index;
    }
}
