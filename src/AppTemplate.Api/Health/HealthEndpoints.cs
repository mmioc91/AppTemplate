using AppTemplate.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace AppTemplate.Api.Health;

// ADR 0019. Liveness has no dependencies, so a database outage never restarts every instance. Readiness takes the
// instance out of rotation (and gates a deploy) until the database is reachable and fully migrated.
internal static class HealthEndpoints
{
    public const string Live = "/health/live";
    public const string Ready = "/health/ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Probes are anonymous.
        app.MapHealthChecks(Live, new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks(Ready, new HealthCheckOptions { Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag) })
            .AllowAnonymous();
        return app;
    }
}
