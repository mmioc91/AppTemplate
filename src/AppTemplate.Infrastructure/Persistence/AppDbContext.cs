using AppTemplate.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Persistence;

// Internal: nothing outside Infrastructure may reach the database around the repositories (tests via InternalsVisibleTo).
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Smart enums and plain enums are stored by name (ADR 0005) in columns of this length.
    public const int EnumColumnLength = 32;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

        // Register every strongly-typed ID and enum here, once, e.g.:
        //   configurationBuilder.Properties<OrderId>().HaveConversion<OrderIdConverter>();
        //   configurationBuilder.Properties<OrderStatus>()
        //       .HaveConversion<EnumerationConverter<OrderStatus>>().HaveMaxLength(EnumColumnLength);
        //   configurationBuilder.Properties<Currency>().HaveConversion<string>().HaveMaxLength(EnumColumnLength);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
