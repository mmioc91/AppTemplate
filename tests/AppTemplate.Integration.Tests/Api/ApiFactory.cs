using AppTemplate.Infrastructure;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppTemplate.Integration.Tests.Api;

// The real Api on a real Postgres. The connection string goes in through configuration, so the real
// AddInfrastructure registers AppDbContext exactly as in production (tests/AppTemplate.Integration.Tests/CLAUDE.md).
// A TimeProvider replaces the system clock for the whole host: the factory's ConfigureServices runs after Program.cs
// has registered everything, and AddApplication only adds the system clock when none is there, so the last
// registration wins (it must stay AddSingleton, not TryAdd). Settings reach the host as command-line settings, above
// appsettings files and environment variables.
internal sealed class ApiFactory(
    string connectionString,
    string environment,
    TimeProvider? time = null,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    // The collection's migrated database, in Development — startup migrations and OpenAPI run as they do locally —
    // with an optional clock and extra configuration.
    public ApiFactory(PostgresFixture postgres, TimeProvider? time = null, IReadOnlyDictionary<string, string>? settings = null)
        : this(postgres.ConnectionString, Environments.Development, time, settings)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment(environment)
            .UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString)
            .ConfigureServices(services =>
            {
                if (time is not null)
                {
                    services.AddSingleton(time);
                }
            });

        foreach ((string key, string value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }
}
