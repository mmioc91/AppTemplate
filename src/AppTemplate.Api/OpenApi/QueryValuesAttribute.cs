namespace AppTemplate.Api.OpenApi;

// The values a string query parameter takes, listed as its enum in the OpenAPI document (QueryValuesTransformer,
// ADR 0027). Documentation only: the use case's validator still decides (ADR 0022). AllowedValuesAttribute would say
// the same, but the OpenAPI generator fails on it.
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
internal sealed class QueryValuesAttribute(params string[] values) : Attribute
{
    public IReadOnlyList<string> Values { get; } = values;
}
