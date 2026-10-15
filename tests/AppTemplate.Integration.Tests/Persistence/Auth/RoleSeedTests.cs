using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Persistence;
using AppTemplate.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

// The seeding hook runs after every Migrate (ADR 0031). The template seeds no role, so the expected rows are whatever
// RoleSeed holds — once a project adds its roles there, these tests pin them (a project with roles also adds the
// check that a second Migrate writes none: the concurrency stamps stay the same).
[Collection(PostgresCollection.Name)]
public sealed class RoleSeedTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrate_EmptyDatabase_SeedsExactlyTheRolesOfTheSeed()
    {
        // Arrange: a database of its own, so the seed is observed on a fresh schema.
        await using ServiceProvider services = postgres.CreateServices(await postgres.CreateEmptyDatabaseAsync());

        // Act
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        // Assert: read back in a new scope.
        await using AsyncServiceScope reader = services.CreateAsyncScope();
        List<IdentityRole<Guid>> roles = await reader.ServiceProvider.GetRequiredService<AppDbContext>().Roles.ToListAsync();
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
}
