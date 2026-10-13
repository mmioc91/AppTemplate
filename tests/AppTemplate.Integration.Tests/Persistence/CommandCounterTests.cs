using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence;

// The counter rides on EF Core's diagnostic events; this proves they still fire and that another context's commands
// stay out of the count.
[Collection(PostgresCollection.Name)]
public sealed class CommandCounterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Count_CommandsOfTwoContexts_CountsOnlyItsOwn()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices();
        await using AsyncServiceScope mine = services.CreateAsyncScope();
        await using AsyncServiceScope other = services.CreateAsyncScope();
        AppDbContext db = mine.ServiceProvider.GetRequiredService<AppDbContext>();
        AppDbContext otherDb = other.ServiceProvider.GetRequiredService<AppDbContext>();
        using CommandCounter counter = new(db);

        // Act
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        await otherDb.Database.ExecuteSqlRawAsync("SELECT 1");
        await db.Database.ExecuteSqlRawAsync("SELECT 1");

        // Assert
        counter.Count.ShouldBe(2);
    }
}
