using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence;

// Migrations are made from the design-time factory's model and applied to the Api's: the two must be one model, or
// `migrations add` reports no change the Api then refuses to start without (PendingModelChangesWarning).
[Collection(PostgresCollection.Name)]
public sealed class DesignTimeDbContextFactoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CreateDbContext_BuildsTheModelTheApiRuns()
    {
        // Arrange
        await using AppDbContext design = new DesignTimeDbContextFactory().CreateDbContext([]);
        await using ServiceProvider services = postgres.CreateServices();
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext runtime = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        bool differs = runtime.GetService<IMigrationsModelDiffer>().HasDifferences(
            design.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            runtime.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        // Assert
        differs.ShouldBeFalse();
    }
}
