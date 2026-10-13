using System.Text.Json;
using System.Text.Json.Nodes;
using AppTemplate.Api.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// No server needed: the transformer sees one schema and the CLR type behind it (ADR 0027).
public class ClientTypesTransformerTests
{
    public enum Shade
    {
        Light,
        Dark,
    }

    [Fact]
    public async Task TransformAsync_Instant_GetsTheStringType()
    {
        // Arrange: what the generator emits for a type with a custom converter.
        OpenApiSchema schema = new() { Format = "date-time" };

        // Act
        await TransformAsync(schema, typeof(DateTimeOffset));

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Format.ShouldBe("date-time");
    }

    [Fact]
    public async Task TransformAsync_NullableInstant_GetsTheNullableStringType()
    {
        // Arrange
        OpenApiSchema schema = new() { Format = "date-time" };

        // Act
        await TransformAsync(schema, typeof(DateTimeOffset?));

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
    }

    [Fact]
    public async Task TransformAsync_EnumWithNullAmongItsValues_IsAStringWithoutNull()
    {
        // Arrange: a nullable use that came first lists null among the names.
        OpenApiSchema schema = new() { Enum = [JsonValue.Create("Light"), JsonValue.Create("Dark"), null!] };

        // Act
        await TransformAsync(schema, typeof(Shade?));

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Enum!.Select(value => value.GetValue<string>()).ShouldBe(["Light", "Dark"]);
    }

    [Fact]
    public async Task TransformAsync_AnythingElse_IsLeftAlone()
    {
        // Arrange
        OpenApiSchema schema = new() { Type = JsonSchemaType.Integer };

        // Act
        await TransformAsync(schema, typeof(int));

        // Assert
        schema.Type.ShouldBe(JsonSchemaType.Integer);
        schema.Enum.ShouldBeNull();
    }

    private static Task TransformAsync(OpenApiSchema schema, Type type) =>
        new ClientTypesTransformer().TransformAsync(
            schema,
            new OpenApiSchemaTransformerContext
            {
                DocumentName = "v1",
                JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(type),
                JsonPropertyInfo = null,
                ParameterDescription = null,
                ApplicationServices = new ServiceCollection().BuildServiceProvider(),
            },
            CancellationToken.None);
}
