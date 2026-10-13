using System.Text.Json;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// The rules behind OpenApiContractTests, on hand-written documents (ADR 0027): each rule reports the one violation
// its fixture holds, by path, and a clean document reports nothing — so the rules can go red before the first
// endpoint exists, and a walker that stopped descending into items, oneOf or a parameter's schema would show here.
public class OpenApiContractRulesTests
{
    [Fact]
    public void NumbersThatAreAlsoStrings_NumberReadableFromAString_ReportsItsPath()
    {
        // Arrange: what the Web JSON defaults put in the document for every number.
        using var document = JsonDocument.Parse(
            """{"components":{"schemas":{"Page":{"type":"object","properties":{"total":{"type":["integer","string"],"pattern":"^-?\\d+$"},"page":{"type":"integer"}}}}}}""");

        // Act
        IEnumerable<string> violations = OpenApiContract.NumbersThatAreAlsoStrings(document.RootElement);

        // Assert
        violations.ShouldBe(["/components/schemas/Page/properties/total"]);
    }

    [Fact]
    public void FormattedSchemasWithoutAType_FormatAlone_ReportsItsPath()
    {
        // Arrange: what the generator writes for a type with a custom converter.
        using var document = JsonDocument.Parse(
            """{"components":{"schemas":{"Row":{"type":"object","properties":{"createdAt":{"format":"date-time"},"name":{"type":"string"}}}}}}""");

        // Act
        IEnumerable<string> violations = OpenApiContract.FormattedSchemasWithoutAType(document.RootElement);

        // Assert
        violations.ShouldBe(["/components/schemas/Row/properties/createdAt"]);
    }

    [Theory]
    [InlineData("""{"type":"string","enum":["Light","Dark",null]}""")]
    [InlineData("""{"enum":["Light","Dark"]}""")]
    [InlineData("""{"type":["string","null"],"enum":["Light","Dark"]}""")]
    public void EnumsThatAreNotStringsWithoutNull_EnumWithNullOrWithoutTheStringType_ReportsItsPath(string shade)
    {
        // Arrange
        using var document = JsonDocument.Parse("""{"components":{"schemas":{"Shade":""" + shade + "}}}");

        // Act
        IEnumerable<string> violations = OpenApiContract.EnumsThatAreNotStringsWithoutNull(document.RootElement);

        // Assert
        violations.ShouldBe(["/components/schemas/Shade"]);
    }

    [Fact]
    public void QueryParametersNotInCamelCase_PascalCaseQueryParameter_ReportsItButNotARouteParameter()
    {
        // Arrange: a record's property bound without [FromQuery(Name = ...)], next to a route parameter.
        using var document = JsonDocument.Parse(
            """{"paths":{"/api/v1/orders/{Id}":{"get":{"parameters":[{"name":"Id","in":"path","schema":{"type":"string"}},{"name":"Page","in":"query","schema":{"type":"integer"}},{"name":"pageSize","in":"query","schema":{"type":"integer"}}]}}}}""");

        // Act
        IEnumerable<string> violations = OpenApiContract.QueryParametersNotInCamelCase(document.RootElement);

        // Assert
        violations.ShouldBe(["Page"]);
    }

    // The walker reaches a schema wherever the document can hold one.
    [Theory]
    [InlineData("""{"components":{"schemas":{"List":{"type":"array","items":{"format":"date-time"}}}}}""", "/components/schemas/List/items")]
    [InlineData("""{"components":{"schemas":{"Either":{"oneOf":[{"type":"string"},{"format":"date-time"}]}}}}""", "/components/schemas/Either/oneOf[1]")]
    [InlineData("""{"components":{"schemas":{"Map":{"type":"object","additionalProperties":{"format":"date-time"}}}}}""", "/components/schemas/Map/additionalProperties")]
    [InlineData("""{"paths":{"/x":{"get":{"parameters":[{"name":"from","in":"query","schema":{"format":"date-time"}}]}}}}""", "/paths//x/get/parameters[0]/schema")]
    [InlineData("""{"paths":{"/x":{"post":{"requestBody":{"content":{"application/json":{"schema":{"format":"date-time"}}}}}}}}""", "/paths//x/post/requestBody/content/application/json/schema")]
    public void FormattedSchemasWithoutAType_SchemaAnywhereInTheDocument_IsFound(string json, string expectedPath)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);

        // Act
        IEnumerable<string> violations = OpenApiContract.FormattedSchemasWithoutAType(document.RootElement);

        // Assert
        violations.ShouldBe([expectedPath]);
    }

    [Fact]
    public void Rules_CleanDocument_ReportNothing()
    {
        // Arrange: what the Api produces for an instant, an enum, a number and a paged query.
        using var document = JsonDocument.Parse(
            """
            {
              "paths": {"/api/v1/orders": {"get": {"parameters": [{"name": "page", "in": "query", "schema": {"type": "integer"}}]}}},
              "components": {"schemas": {
                "Order": {"type": "object", "properties": {
                  "createdAt": {"type": "string", "format": "date-time"},
                  "shade": {"type": "string", "enum": ["Light", "Dark"]},
                  "total": {"type": "integer"},
                  "lines": {"type": "array", "items": {"type": "string"}}
                }}
              }}
            }
            """);

        // Act & Assert
        OpenApiContract.NumbersThatAreAlsoStrings(document.RootElement).ShouldBeEmpty();
        OpenApiContract.FormattedSchemasWithoutAType(document.RootElement).ShouldBeEmpty();
        OpenApiContract.EnumsThatAreNotStringsWithoutNull(document.RootElement).ShouldBeEmpty();
        OpenApiContract.QueryParametersNotInCamelCase(document.RootElement).ShouldBeEmpty();
    }
}
