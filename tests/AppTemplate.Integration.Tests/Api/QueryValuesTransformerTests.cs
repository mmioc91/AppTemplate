using System.Reflection;
using AppTemplate.Api.OpenApi;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// No server needed: the transformer sees the operation and the endpoint's parameter descriptions (ADR 0027).
public class QueryValuesTransformerTests
{
    [Fact]
    public async Task TransformAsync_ParameterWithQueryValues_ListsThemAsItsEnum()
    {
        // Arrange
        OpenApiOperation operation = Operation("status", ParameterLocation.Query);

        // Act
        await TransformAsync(operation, Described("status", parameterIndex: 0));

        // Assert
        operation.Parameters![0].Schema!.Enum!.Select(value => value.GetValue<string>()).ShouldBe(["active", "archived"]);
    }

    [Fact]
    public async Task TransformAsync_ParameterWithoutTheAttribute_IsLeftAlone()
    {
        // Arrange
        OpenApiOperation operation = Operation("sort", ParameterLocation.Query);

        // Act
        await TransformAsync(operation, Described("sort", parameterIndex: 1));

        // Assert
        operation.Parameters![0].Schema!.Enum.ShouldBeNull();
    }

    [Fact]
    public async Task TransformAsync_RouteParameterOfTheSameName_IsNotTouched()
    {
        // Arrange: the attribute is for query strings; a route segment that happens to share the name stays as it is.
        OpenApiOperation operation = Operation("status", ParameterLocation.Path);

        // Act
        await TransformAsync(operation, Described("status", parameterIndex: 0));

        // Assert
        operation.Parameters![0].Schema!.Enum.ShouldBeNull();
    }

    // The shapes a Minimal API endpoint declares: a query parameter with fixed values, and one without.
    internal static string Probe([QueryValues("active", "archived")] string status, string sort) => status + sort;

    private static OpenApiOperation Operation(string name, ParameterLocation location) => new()
    {
        Parameters = [new OpenApiParameter { Name = name, In = location, Schema = new OpenApiSchema { Type = JsonSchemaType.String } }],
    };

    private static ApiParameterDescription Described(string name, int parameterIndex) => new()
    {
        Name = name,
        Source = BindingSource.Query,
        ParameterDescriptor = new ProbeDescriptor(
            typeof(QueryValuesTransformerTests).GetMethod(nameof(Probe), BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[parameterIndex]),
    };

    private static Task TransformAsync(OpenApiOperation operation, ApiParameterDescription parameter)
    {
        ApiDescription description = new();
        description.ParameterDescriptions.Add(parameter);

        return new QueryValuesTransformer().TransformAsync(
            operation,
            new OpenApiOperationTransformerContext
            {
                DocumentName = "v1",
                Description = description,
                ApplicationServices = new ServiceCollection().BuildServiceProvider(),
            },
            CancellationToken.None);
    }

    // What the endpoint metadata provider gives a Minimal API parameter: a descriptor that carries its ParameterInfo.
    private sealed class ProbeDescriptor(ParameterInfo parameterInfo) : ParameterDescriptor, IParameterInfoParameterDescriptor
    {
        public ParameterInfo ParameterInfo { get; } = parameterInfo;
    }
}
