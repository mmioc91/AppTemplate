using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppTemplate.Infrastructure.Persistence.Converters;

// Npgsql only writes DateTimeOffset with offset 0 to timestamptz; a client time like 10:00+02:00 would fail the save.
// Converting to UTC keeps the same instant (DateTimeOffset equality compares instants).
internal sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, DateTimeOffset>(value => value.ToUniversalTime(), value => value);
