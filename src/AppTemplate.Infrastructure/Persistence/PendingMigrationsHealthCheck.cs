using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppTemplate.Infrastructure.Persistence;

// Not ready while the database lacks a migration this build expects — e.g. a deploy that skipped the migration step
// (production applies migrations separately, ADR 0019).
internal sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        return pending.Any()
            ? HealthCheckResult.Unhealthy("The database has pending migrations.")
            : HealthCheckResult.Healthy();
    }
}
