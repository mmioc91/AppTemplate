using System.Text.Json;
using AppTemplate.Integration.Tests.Persistence;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// The OpenAPI document a client is generated from (ADR 0009, 0027): its types must be the ones the client should use.
// Every rule walks the whole document — components, inline schemas, parameters, items, oneOf — so a new endpoint can't
// slip past it. With no endpoint yet the document is empty and there is nothing to check; the first one makes these
// real.
[Collection(PostgresCollection.Name)]
public sealed class OpenApiContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(postgres);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Document_NumbersAreNeverAlsoStrings()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert: NSwag would type them number | string.
        Schemas(document.RootElement)
            .Where(s => Types(s.Schema).Contains("string") && (Types(s.Schema).Contains("integer") || Types(s.Schema).Contains("number")))
            .Select(s => s.Path)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_EveryFormattedSchemaHasAType()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert: a format without a type (an instant through a custom converter) is `any` in the client.
        Schemas(document.RootElement)
            .Where(s => s.Schema.TryGetProperty("format", out _) && Types(s.Schema).Count == 0)
            .Select(s => s.Path)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_EveryEnumIsAStringWithoutNull()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert: otherwise NSwag generates an empty interface instead of the enum; nullability belongs on the property.
        Schemas(document.RootElement)
            .Where(s => s.Schema.TryGetProperty("enum", out JsonElement values)
                && (!Types(s.Schema).SetEquals(["string"]) || values.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.Null)))
            .Select(s => s.Path)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_QueryParameters_AreCamelCase()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert
        QueryParameters(document)
            .Select(p => p.Name)
            .Where(name => JsonNamingPolicy.CamelCase.ConvertName(name) != name)
            .ShouldBeEmpty();
    }

    private static HashSet<string?> Types(JsonElement schema) =>
        !schema.TryGetProperty("type", out JsonElement type) ? []
        : type.ValueKind == JsonValueKind.Array ? [.. type.EnumerateArray().Select(t => t.GetString())]
        : [type.GetString()];

    // Every object in the document that is a schema: the value of a "schema", "items", "additionalProperties" or
    // "not" key, an entry of "properties", "schemas", "oneOf", "anyOf" or "allOf" — at any depth.
    private static IEnumerable<(string Path, JsonElement Schema)> Schemas(JsonElement element, string path = "", bool isSchema = false)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;

            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach ((string, JsonElement) found in Schemas(item, $"{path}[{index++}]", isSchema))
                {
                    yield return found;
                }
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (isSchema)
        {
            yield return (path, element);
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string childPath = $"{path}/{property.Name}";
            bool childIsSchema = property.Name is "schema" or "items" or "additionalProperties" or "not";
            bool childrenAreSchemas = property.Name is "properties" or "schemas" or "oneOf" or "anyOf" or "allOf";

            if (childrenAreSchemas && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty entry in property.Value.EnumerateObject())
                {
                    foreach ((string, JsonElement) found in Schemas(entry.Value, $"{childPath}/{entry.Name}", isSchema: true))
                    {
                        yield return found;
                    }
                }
            }
            else
            {
                foreach ((string, JsonElement) found in Schemas(property.Value, childPath, childIsSchema || childrenAreSchemas))
                {
                    yield return found;
                }
            }
        }
    }

    private static IEnumerable<(string Name, JsonElement Schema)> QueryParameters(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(operation => operation.Value.TryGetProperty("parameters", out _))
            .SelectMany(operation => operation.Value.GetProperty("parameters").EnumerateArray())
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => (parameter.GetProperty("name").GetString()!, parameter.GetProperty("schema")));

    private async Task<JsonDocument> DocumentAsync()
    {
        using HttpClient client = _factory.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
    }
}
