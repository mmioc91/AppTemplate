using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Db";

    // Set to true, the Api migrates the database and exits instead of serving: the production deploy step (ADR 0029).
    public const string MigrateAndExitKey = "Database:MigrateAndExit";

    // Health checks tagged with this decide readiness (ADR 0019): database reachable and fully migrated.
    public const string ReadinessTag = "ready";

    private const string DatabaseHealthCheckName = "database";
    private const string MigrationsHealthCheckName = "migrations";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing (set it with dotnet user-secrets locally).");

        // Not pooled: a context that ever holds per-request state (e.g. a tenant filter) would leak it across requests.
        services.AddDbContext<AppDbContext>(options => Configure(options, connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName, tags: [ReadinessTag])
            .AddCheck<PendingMigrationsHealthCheck>(MigrationsHealthCheckName, tags: [ReadinessTag]);

        return services;
    }

    // Development only (the Api calls it on startup); production applies migrations as a separate deploy step.
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
    }

    // Shared with the design-time factory so migrations use exactly the runtime configuration.
    internal static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention();
}
