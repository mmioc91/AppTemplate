using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Mail;
using AppTemplate.Infrastructure.Persistence;
using AppTemplate.Infrastructure.Persistence.Seeding;
using AppTemplate.Infrastructure.Web;
using Microsoft.AspNetCore.Identity;
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

        // Identity core only — no cookies, no SignInManager; the Api adds the JWT bearer scheme (ADR 0031). JwtOptions
        // is bound by the Api.
        services.AddIdentityCore<ApplicationUser>(ConfigureIdentity)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, TokenService>();

        // Mail leaves through the outbox (ADR 0034): queued with the change, sent by the worker; the links it carries
        // point at the web app. MailOptions and WebOptions are bound by the Api.
        services.AddScoped<IMailQueue, MailQueue>();
        services.AddSingleton<IMailRenderer, HandlebarsMailRenderer>();
        services.AddScoped<MailDelivery>();
        services.AddSingleton<IMailTransport, SmtpMailTransport>();
        services.AddHostedService<MailDeliveryWorker>();
        services.AddSingleton<IWebLinks, WebLinks>();

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

    // Shared with the design-time factory so migrations use exactly the runtime configuration. The seed runs after
    // every Migrate — on startup here, under `dotnet ef database update` through the factory — so reference data
    // reaches every environment with its schema (ADR 0031). Only on a database at the latest migration: a migration
    // reverted (`database update 0`, `migrations remove`) or stopped short of the last one has no table the seed may
    // expect.
    // `roles` stands in for RoleSeed.All in tests, so they can see the hook seed a row through Migrate.
    internal static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        Func<IReadOnlyList<IdentityRole<Guid>>>? roles = null) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention()
            .UseSeeding((db, _) =>
            {
                if (!db.Database.GetPendingMigrations().Any())
                {
                    RoleSeeder.Seed(db, (roles ?? RoleSeed.All)());
                }
            })
            .UseAsyncSeeding(async (db, _, cancellationToken) =>
            {
                if (!(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    await RoleSeeder.SeedAsync(db, (roles ?? RoleSeed.All)(), cancellationToken);
                }
            });

    // Length beats character classes, so the only password rule is the minimum length (ADR 0031). Lockout from the
    // first failed attempt of a new account. The user name is the email, which the validators and Identity's email rule
    // already check; Identity's user-name alphabet would refuse valid addresses such as o'brien@example.com. Internal so
    // the design-time factory builds the model with the same store options (a schema version, key lengths).
    internal static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = string.Empty;

        options.Password.RequiredLength = AuthRules.PasswordMinLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = IdentityService.MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = IdentityService.LockoutDuration;
    }
}
