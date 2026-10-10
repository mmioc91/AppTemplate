using AppTemplate.Infrastructure;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace AppTemplate.Integration.Tests.Api;

// The real Api on a real Postgres. The connection string goes in through configuration, so the real
// AddInfrastructure registers AppDbContext exactly as in production (tests/AppTemplate.Integration.Tests/CLAUDE.md).
internal sealed class ApiFactory(string connectionString, string environment) : WebApplicationFactory<Program>
{
    // The collection's migrated database, in Development — startup migrations and OpenAPI run as they do locally.
    public ApiFactory(PostgresFixture postgres)
        : this(postgres.ConnectionString, Environments.Development)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseEnvironment(environment)
            .UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
}
