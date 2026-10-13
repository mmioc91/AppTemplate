using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AppTemplate.Api.OpenApi;

// Gives the generated client real types where the schemas would leave it guessing (ADR 0027):
// - an instant is a string in date-time format: StrictInstantConverter is a custom converter, so the generator emits
//   the format without a type, which NSwag turns into `any`;
// - an enum is a string with its names only: the generator leaves the type out, and lists null among the values when
//   a nullable use came first, which NSwag turns into an empty interface. Nullability stays where it belongs, on the
//   property (oneOf null | $ref).
internal sealed class ClientTypesTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Type type = context.JsonTypeInfo.Type;
        Type? underlying = Nullable.GetUnderlyingType(type);

        if ((underlying ?? type) == typeof(DateTimeOffset))
        {
            schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
        }
        else if ((underlying ?? type).IsEnum && schema.Enum is { Count: > 0 } values)
        {
            schema.Type = JsonSchemaType.String;
            schema.Enum = [.. values.Where(value => value is not null)];
        }

        return Task.CompletedTask;
    }
}
