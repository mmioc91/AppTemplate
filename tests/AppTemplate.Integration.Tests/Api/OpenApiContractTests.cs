using System.Text.Json;
using AppTemplate.Integration.Tests.Persistence;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// The OpenAPI document a client is generated from (ADR 0009, 0027) keeps the rules in OpenApiContract. With no
// endpoint yet the document is empty and these have nothing to find; OpenApiContractRulesTests proves the rules
// themselves, and the first endpoint makes these real.
[Collection(PostgresCollection.Name)]
public sealed class OpenApiContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(postgres);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Document_NumbersAreNeverAlsoStrings()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert
        OpenApiContract.NumbersThatAreAlsoStrings(document.RootElement).ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_EveryFormattedSchemaHasAType()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert
        OpenApiContract.FormattedSchemasWithoutAType(document.RootElement).ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_EveryEnumIsAStringWithoutNull()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert
        OpenApiContract.EnumsThatAreNotStringsWithoutNull(document.RootElement).ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_QueryParameters_AreCamelCase()
    {
        // Act
        using JsonDocument document = await DocumentAsync();

        // Assert
        OpenApiContract.QueryParametersNotInCamelCase(document.RootElement).ShouldBeEmpty();
    }

    private async Task<JsonDocument> DocumentAsync()
    {
        using HttpClient client = _factory.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
    }
}
