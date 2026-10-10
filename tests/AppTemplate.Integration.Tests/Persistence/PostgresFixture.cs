using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AppTemplate.Integration.Tests.Persistence;

// One real Postgres (same major version as docker-compose.yml) for the whole collection, migrated once — which is
// also the test that the migrations apply to an empty database. Tests isolate themselves with fresh ids instead of
// resetting the database.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using ServiceProvider services = CreateServices();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    internal string ConnectionString => _container.GetConnectionString();

    // Built through the real AddInfrastructure, as the Api does. Read back in a new scope so values come from
    // Postgres, not the change tracker.
    internal ServiceProvider CreateServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = ConnectionString,
            })
            .Build();

        ServiceCollection services = new();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
