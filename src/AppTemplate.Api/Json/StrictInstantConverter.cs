using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppTemplate.Api.Json;

// An instant says where on the clock it is (ADR 0027): "2026-06-14T10:00:00Z" or "2026-06-14T12:00:00+02:00".
// Without an offset System.Text.Json reads it in the server's time zone — a different instant on a laptop and in a
// UTC container, with no error anywhere — so such a value, a bare date included, is malformed JSON (400
// MALFORMED_REQUEST). Applies to DateTimeOffset? as well; written as before.
internal sealed class StrictInstantConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // An offset reads as a UTC or local DateTime, its absence as Unspecified.
        if (reader.TokenType == JsonTokenType.String
            && reader.TryGetDateTime(out DateTime clock)
            && clock.Kind != DateTimeKind.Unspecified
            && reader.TryGetDateTimeOffset(out DateTimeOffset instant))
        {
            return instant;
        }

        throw new JsonException("An instant needs an ISO 8601 date and time with an offset (Z or +hh:mm).");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
