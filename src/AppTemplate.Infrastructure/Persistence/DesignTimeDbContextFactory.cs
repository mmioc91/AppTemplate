using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AppTemplate.Infrastructure.Persistence;

// Used only by `dotnet ef`, so migrations don't depend on the Api being wired. Adding a migration never connects to a
// database; applying one reads the ConnectionStrings__Db environment variable.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string NoDatabase = "Host=localhost;Database=apptemplate";

    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Db") ?? NoDatabase;

        DbContextOptionsBuilder<AppDbContext> options = new();
        DependencyInjection.Configure(options, connectionString);

        return new AppDbContext(options.Options);
    }
}
