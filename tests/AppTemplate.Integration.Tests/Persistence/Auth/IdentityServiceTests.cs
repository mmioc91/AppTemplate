using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

[Collection(PostgresCollection.Name)]
public sealed class IdentityServiceTests(PostgresFixture postgres)
{
    // A role the template doesn't have, standing in for a project's; created when the database lacks it.
    private const string Tester = "Tester";

    [Fact]
    public async Task CreateUser_NewEmail_StoresAPlainAccount()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, email);

        // Assert: read back in a new scope.
        outcome.ShouldBe(CreateUserOutcome.Created);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IIdentityService identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        (await identity.FindByIdAsync(id, CancellationToken.None)).ShouldBe(new UserAccount(id, email, "Ana"));
        (await identity.GetRolesAsync(id, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateUser_WithARole_StoresTheUserInThatRole()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        await EnsureRoleAsync(services, Tester);
        var id = Guid.CreateVersion7();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, $"user-{id:N}@example.com", role: Tester);

        // Assert
        outcome.ShouldBe(CreateUserOutcome.Created);
        (await RolesAsync(services, id)).ShouldBe([Tester]);
    }

    [Fact]
    public async Task CreateUser_OwnRowAlreadyThere_IsCreated()
    {
        // Arrange: what a retried commit looks like — the row with our id exists before the insert runs, so the insert
        // hits the primary key; Identity's email pre-check passes because the email is new to it (ADR 0013).
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new ApplicationUser { Id = id, UserName = $"other-{id:N}", NormalizedUserName = $"OTHER-{id:N}".ToUpperInvariant(), DisplayName = "Ana" });
            await db.SaveChangesAsync();
        }

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, email);

        // Assert
        outcome.ShouldBe(CreateUserOutcome.Created);
    }

    [Fact]
    public async Task CreateUser_EmailTakenInAnotherCasing_IsEmailTaken()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var first = Guid.CreateVersion7();
        string email = $"user-{first:N}@example.com";
        (await CreateAsync(services, first, email)).ShouldBe(CreateUserOutcome.Created);

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, Guid.CreateVersion7(), email.ToUpperInvariant());

        // Assert
        outcome.ShouldBe(CreateUserOutcome.EmailTaken);
    }

    [Fact]
    public async Task CreateUser_PasswordUnderTheMinimum_IsPasswordTooShort()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, $"user-{id:N}@example.com", new string('p', AuthRules.PasswordMinLength - 1));

        // Assert: nothing was written.
        outcome.ShouldBe(CreateUserOutcome.PasswordTooShort);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(user => user.Id == id)).ShouldBeFalse();
    }

    [Fact]
    public async Task CheckPassword_RightPassword_IsTheAccount()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);

        // Act: the lookup is case-insensitive, like registration.
        UserAccount? account = await CheckAsync(services, email.ToUpperInvariant(), AuthServices.Password);

        // Assert
        account.ShouldBe(new UserAccount(id, email, "Ana"));
    }

    [Fact]
    public async Task CheckPassword_WrongPassword_IsNull()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);

        // Act
        UserAccount? account = await CheckAsync(services, email, "wrong horse battery");

        // Assert
        account.ShouldBeNull();
    }

    [Fact]
    public async Task CheckPassword_UnknownEmail_IsNullAtTheCostOfAHash()
    {
        // Arrange: no account, so nothing real to verify against — the refusal still runs the hasher once, so its
        // timing says nothing about whether the email is registered (ADR 0031).
        CountingPasswordHasher hasher = new();
        await using ServiceProvider services = postgres.CreateServices(services => Configure(services, hasher));

        // Act
        UserAccount? account = await CheckAsync(services, $"nobody-{Guid.CreateVersion7():N}@example.com", AuthServices.Password);

        // Assert
        account.ShouldBeNull();
        hasher.Verifications.ShouldBe(1);
    }

    [Fact]
    public async Task CheckPassword_TooManyWrongAttempts_LocksOutAndTheRightPasswordFailsAtTheCostOfAHash()
    {
        // Arrange: every wrong attempt verifies the real hash; the locked-out attempt has nothing to verify and runs
        // the hasher anyway.
        CountingPasswordHasher hasher = new();
        await using ServiceProvider services = postgres.CreateServices(services => Configure(services, hasher));
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);
        for (int attempt = 0; attempt < IdentityService.MaxFailedAccessAttempts; attempt++)
        {
            (await CheckAsync(services, email, "wrong horse battery")).ShouldBeNull();
        }

        // Act
        UserAccount? account = await CheckAsync(services, email, AuthServices.Password);

        // Assert
        account.ShouldBeNull();
        hasher.Verifications.ShouldBe(IdentityService.MaxFailedAccessAttempts + 1);
    }

    [Fact]
    public async Task CheckPassword_RightPasswordAfterWrongOnes_SucceedsAndResetsTheCount()
    {
        // Arrange: one attempt short of the lockout.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);
        for (int attempt = 0; attempt < IdentityService.MaxFailedAccessAttempts - 1; attempt++)
        {
            await CheckAsync(services, email, "wrong horse battery");
        }

        // Act
        UserAccount? right = await CheckAsync(services, email, AuthServices.Password);
        UserAccount? wrongAgain = await CheckAsync(services, email, "wrong horse battery");
        UserAccount? rightAgain = await CheckAsync(services, email, AuthServices.Password);

        // Assert: the count started over, so one more wrong attempt is a plain failure, not a lockout.
        right.ShouldNotBeNull();
        wrongAgain.ShouldBeNull();
        rightAgain.ShouldNotBeNull();
    }

    private static void Configure(IServiceCollection services, CountingPasswordHasher hasher)
    {
        AuthServices.Configure(services);
        services.AddSingleton<IPasswordHasher<ApplicationUser>>(hasher);
    }

    private static async Task<CreateUserOutcome> CreateAsync(ServiceProvider services, Guid id, string email, string password = AuthServices.Password, string? role = null)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .CreateUserAsync(id, email, password, "Ana", role, CancellationToken.None);
    }

    private static async Task<UserAccount?> CheckAsync(ServiceProvider services, string email, string password)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().CheckPasswordAsync(email, password, CancellationToken.None);
    }

    // Read back in a new scope, so the roles come from Postgres.
    private static async Task<IReadOnlyList<string>> RolesAsync(ServiceProvider services, Guid id)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().GetRolesAsync(id, CancellationToken.None);
    }

    private static async Task EnsureRoleAsync(ServiceProvider services, string role)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        RoleManager<IdentityRole<Guid>> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role))
        {
            (await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() })).Succeeded.ShouldBeTrue();
        }
    }

    // The configured hasher, counting the verifications: the proof that a refusal costs a hash whether or not there is
    // a real one to check.
    private sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> _inner = new();

        public int Verifications { get; private set; }

        public string HashPassword(ApplicationUser user, string password) => _inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            Verifications++;
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
