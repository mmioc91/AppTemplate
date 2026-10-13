using AppTemplate.Infrastructure;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppTemplate.Integration.Tests.Api;

// The real Api on a real Postgres. The connection string goes in through configuration, so the real
// AddInfrastructure registers AppDbContext exactly as in production (tests/AppTemplate.Integration.Tests/CLAUDE.md).
// A TimeProvider replaces the system clock for the whole host (it is registered after the app's own services, and
// AddApplication only adds the system clock when none is there), so a test can move time. Settings add or override
// configuration the way an environment variable would.
internal sealed class ApiFactory(
    string connectionString,
    string environment,
    TimeProvider? time = null,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    // The collection's migrated database, in Development — startup migrations and OpenAPI run as they do locally.
    public ApiFactory(PostgresFixture postgres, TimeProvider? time = null)
        : this(postgres.ConnectionString, Environments.Development, time)
    {
    }

    // The collection's database with extra configuration.
    public ApiFactory(PostgresFixture postgres, IReadOnlyDictionary<string, string> settings)
        : this(postgres.ConnectionString, Environments.Development, null, settings)
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
