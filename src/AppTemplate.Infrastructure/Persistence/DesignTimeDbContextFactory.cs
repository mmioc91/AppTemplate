using AppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Infrastructure.Persistence;

// Used only by `dotnet ef`, so migrations don't depend on the Api being wired. Adding a migration never connects to a
// database; applying one reads the ConnectionStrings__Db environment variable. Identity shapes its tables from its
// store options (schema version, key lengths), which the model reads from the application's services: the factory
// gives it the same ConfigureIdentity the runtime uses, so a migration is made from the model the Api runs.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string NoDatabase = "Host=localhost;Database=apptemplate";

    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Db") ?? NoDatabase;

        ServiceProvider identity = new ServiceCollection()
            .AddIdentityCore<ApplicationUser>(DependencyInjection.ConfigureIdentity)
            .Services
            .BuildServiceProvider();

        DbContextOptionsBuilder<AppDbContext> options = new();
        DependencyInjection.Configure(options, connectionString).UseApplicationServiceProvider(identity);

        return new AppDbContext(options.Options);
    }
}
