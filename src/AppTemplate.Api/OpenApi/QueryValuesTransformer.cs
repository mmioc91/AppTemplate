using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AppTemplate.Api.OpenApi;

// Lists a [QueryValues] parameter's values as the enum of its schema, so the generated client types it as a union of
// those strings rather than any string (ADR 0027).
internal sealed class QueryValuesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (ApiParameterDescription description in context.Description.ParameterDescriptions)
        {
            if (description.ParameterDescriptor is not IParameterInfoParameterDescriptor { ParameterInfo: var info }
                || info.GetCustomAttribute<QueryValuesAttribute>() is not { } values
                || operation.Parameters?.FirstOrDefault(p => p.In == ParameterLocation.Query && p.Name == description.Name)?.Schema is not OpenApiSchema schema)
            {
                continue;
            }

            schema.Enum = [.. values.Values.Select(value => (JsonNode)JsonValue.Create(value))];
        }

        return Task.CompletedTask;
    }
}
