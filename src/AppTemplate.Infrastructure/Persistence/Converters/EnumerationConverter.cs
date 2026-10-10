using AppTemplate.SharedKernel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppTemplate.Infrastructure.Persistence.Converters;

// Smart enums are stored by Name (e.g. "Delivered") so the column stays readable and survives reordering (ADR 0005).
internal sealed class EnumerationConverter<TEnum>() : ValueConverter<TEnum, string>(e => e.Name, name => FromName(name))
    where TEnum : Enumeration<TEnum>
{
    private static TEnum FromName(string name) =>
        Enumeration<TEnum>.FromName(name)
        ?? throw new InvalidOperationException($"'{name}' is not a known {typeof(TEnum).Name}.");
}
