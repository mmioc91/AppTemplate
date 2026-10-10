using Asp.Versioning;

namespace AppTemplate.Api;

internal static class ApiVersions
{
    public static readonly ApiVersion V1 = new(1);

    // Business routes live under /api/v1; each area maps its own sub-group on it (v1.MapGroup("/orders")).
    // Operational endpoints (/health) and third-party webhooks with a fixed URL stay outside it.
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder app) =>
        app.NewVersionedApi().MapGroup("/api/v{version:apiVersion}").HasApiVersion(V1);
}
