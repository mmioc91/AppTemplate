using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Persistence;
using AppTemplate.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

// The seeding hook runs after every Migrate (ADR 0031). The template seeds no role, so the seeder is proved with a
// test-only row, and the shipped seed with whatever RoleSeed holds — once a project adds its roles there, these pin
// them. Each test gets a database of its own, so the seed is observed on a fresh schema.
[Collection(PostgresCollection.Name)]
public sealed class RoleSeedTests(PostgresFixture postgres)
{
    private static readonly Guid ProbeId = Guid.Parse("01a1231c-0000-7000-8000-0000000001ff");

    [Fact]
    public async Task Migrate_EmptyDatabase_SeedsExactlyTheRolesOfTheSeed()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(await postgres.CreateEmptyDatabaseAsync());

        // Act
        await MigrateAsync(services);

        // Assert
        List<IdentityRole<Guid>> roles = await ReadRolesAsync(services);
        roles.Select(role => (role.Id, role.Name, role.NormalizedName))
            .ShouldBe(RoleSeed.All().Select(role => (role.Id, role.Name, role.NormalizedName)), ignoreOrder: true);
    }

    [Fact]
    public void All_NamesExactlyTheRolesTheApplicationKnows()
    {
        // Act
        IReadOnlyList<IdentityRole<Guid>> seed = RoleSeed.All();

        // Assert: a role constant without a seed row is a role no user can get; a seed row without a constant, one
        // nobody can require. Ids are the well-known keys, so no two rows may share one.
        seed.Select(role => role.Name).ShouldBe(Roles.All, ignoreOrder: true);
        seed.Select(role => role.Id).ShouldBeUnique();
    }

    [Fact]
    public void Configure_WiresTheSeedingHookForBothMigratePaths()
    {
        // Arrange: Migrate runs the synchronous hook (`dotnet ef database update`), MigrateAsync the asynchronous one.
        DbContextOptionsBuilder<AppDbContext> options = new();

        // Act
        DependencyInjection.Configure(options, postgres.ConnectionString);

        // Assert
        CoreOptionsExtension core = options.Options.FindExtension<CoreOptionsExtension>().ShouldNotBeNull();
        core.Seeder.ShouldNotBeNull();
        core.AsyncSeeder.ShouldNotBeNull();
    }

    [Fact]
    public async Task MigrateAsync_EmptyDatabase_SeedsThroughTheGuardedHook()
    {
        // Arrange: the real hook (DependencyInjection.Configure) with a test-only row in place of RoleSeed.
        await using AppDbContext db = ContextSeeding(await postgres.CreateEmptyDatabaseAsync());

        // Act
        await db.Database.MigrateAsync();

        // Assert
        (await db.Roles.AsNoTracking().AnyAsync(role => role.Id == ProbeId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Migrate_EmptyDatabase_SeedsThroughTheGuardedSynchronousHook()
    {
        // Arrange: the path `dotnet ef database update` takes.
        await using AppDbContext db = ContextSeeding(await postgres.CreateEmptyDatabaseAsync());

        // Act: on a thread of its own, as the synchronous tool runs it.
        await Task.Run(() => db.Database.Migrate());

        // Assert
        (await db.Roles.AsNoTracking().AnyAsync(role => role.Id == ProbeId)).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_Twice_InsertsEachRoleOnceAndRewritesNone()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(await postgres.CreateEmptyDatabaseAsync());
        await MigrateAsync(services);
        await SeedAsync(services);
        Dictionary<Guid, string?> stampsAfterFirstRun = await ReadStampsAsync(services);

        // Act
        await SeedAsync(services);

        // Assert: a re-inserted or rewritten role would carry a new concurrency stamp.
        stampsAfterFirstRun.Keys.ShouldContain(ProbeId);
        (await ReadStampsAsync(services)).ShouldBe(stampsAfterFirstRun);
    }

    [Fact]
    public async Task Seed_Synchronous_InsertsTheMissingRole()
    {
        // Arrange: the path `dotnet ef database update` takes.
        await using ServiceProvider services = postgres.CreateServices(await postgres.CreateEmptyDatabaseAsync());
        await MigrateAsync(services);

        // Act
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            RoleSeeder.Seed(scope.ServiceProvider.GetRequiredService<AppDbContext>(), [Probe()]);
        }

        // Assert
        (await ReadRolesAsync(services)).Select(role => role.Id).ShouldContain(ProbeId);
    }

    [Fact]
    public async Task MigrateToZero_FromTheLatestMigration_RevertsAndMigratesAgain()
    {
        // Arrange: after a revert the seed's tables are gone, so the hook must not run on a database short of the
        // latest migration (`database update 0`, `migrations remove`).
        await using ServiceProvider services = postgres.CreateServices(await postgres.CreateEmptyDatabaseAsync());
        await MigrateAsync(services);

        // Act
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>().MigrateAsync("0");
        }

        await MigrateAsync(services);

        // Assert
        (await ReadRolesAsync(services)).Count.ShouldBe(RoleSeed.All().Count);
    }

    [Fact]
    public async Task MigrateToZero_ThenMigrateAgain_SeedsTheRowsAgain()
    {
        // Arrange
        await using AppDbContext db = ContextSeeding(await postgres.CreateEmptyDatabaseAsync());
        await db.Database.MigrateAsync();

        // Act
        await db.GetService<IMigrator>().MigrateAsync("0");
        await db.Database.MigrateAsync();

        // Assert
        (await db.Roles.AsNoTracking().AnyAsync(role => role.Id == ProbeId)).ShouldBeTrue();
    }

    // A fresh instance per call, with its own concurrency stamp, as RoleSeed.All() gives: a seeder that rewrote an
    // existing row would store a new stamp, and the comparison above would go red.
    private static IdentityRole<Guid> Probe() => new("Probe") { Id = ProbeId, NormalizedName = "PROBE" };

    private static AppDbContext ContextSeeding(string connectionString)
    {
        DbContextOptionsBuilder<AppDbContext> options = new();
        DependencyInjection.Configure(options, connectionString, () => [Probe()]);
        return new AppDbContext(options.Options);
    }

    private static async Task MigrateAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private static async Task SeedAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), [Probe()], CancellationToken.None);
    }

    private static async Task<List<IdentityRole<Guid>>> ReadRolesAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.ToListAsync();
    }

    private static async Task<Dictionary<Guid, string?>> ReadStampsAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles
            .ToDictionaryAsync(role => role.Id, role => role.ConcurrencyStamp);
    }
}
