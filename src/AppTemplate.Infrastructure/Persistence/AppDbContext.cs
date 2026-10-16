using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Mail;
using AppTemplate.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Persistence;

// Internal: nothing outside Infrastructure may reach the database around the repositories (tests via InternalsVisibleTo).
// One context for the aggregates and the Identity tables: one migration history, one seed path, one transaction
// (ADR 0031).
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // Smart enums and plain enums are stored by name (ADR 0005) in columns of this length.
    public const int EnumColumnLength = 32;

    // Aggregate roots only (public DbSet<Order> Orders => Set<Order>();); children are reached through their root.

    // Owned by Infrastructure, not an aggregate: the refresh tokens behind ITokenService.
    public DbSet<StoredRefreshToken> RefreshTokens => Set<StoredRefreshToken>();

    // Owned by Infrastructure, not an aggregate: the mail outbox behind IMailQueue (ADR 0034).
    public DbSet<QueuedMail> QueuedMails => Set<QueuedMail>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

        // Register every strongly-typed ID and enum here, once, e.g.:
        //   configurationBuilder.Properties<OrderId>().HaveConversion<OrderIdConverter>();
        //   configurationBuilder.Properties<OrderStatus>()
        //       .HaveConversion<EnumerationConverter<OrderStatus>>().HaveMaxLength(EnumColumnLength);
        //   configurationBuilder.Properties<Currency>().HaveConversion<string>().HaveMaxLength(EnumColumnLength);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own mapping first, so our configurations can refine it.
        base.OnModelCreating(modelBuilder);
        RenameIdentityTables(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    // Identity names its tables (AspNetUsers…) and indexes (UserNameIndex…) explicitly, which the snake_case
    // convention leaves alone, so these are the only hand-written names in the model — spelled as the convention
    // would. Before any foreign key to them is added, so the keys are named after `users`. The email index is made
    // unique: Identity checks that in code, the database makes it hold under concurrent registrations (ADR 0031).
    private static void RenameIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(users =>
        {
            users.ToTable("users");
            users.HasIndex(user => user.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
            users.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("ix_users_normalized_email");
        });
        modelBuilder.Entity<IdentityRole<Guid>>(roles =>
        {
            roles.ToTable("roles");
            roles.HasIndex(role => role.NormalizedName).HasDatabaseName("ix_roles_normalized_name");
        });
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
