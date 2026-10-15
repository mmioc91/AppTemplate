using System.Buffers.Binary;
using System.Data.Common;
using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

[Collection(PostgresCollection.Name)]
public sealed class IdentityServiceTests(PostgresFixture postgres)
{
    // A role the template doesn't have, standing in for a project's; created when the database lacks it.
    private const string Tester = "Tester";

    [Fact]
    public async Task AddInfrastructure_ConfiguresIdentityAsTheAdrSays()
    {
        // Arrange: literals, not the constants, so a changed constant goes red here too (ADR 0031).
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);

        // Act
        IdentityOptions options = services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        options.Lockout.MaxFailedAccessAttempts.ShouldBe(5);
        options.Lockout.DefaultLockoutTimeSpan.ShouldBe(TimeSpan.FromMinutes(15));
        options.Lockout.AllowedForNewUsers.ShouldBeTrue();
        options.Password.RequiredLength.ShouldBe(10);
        (options.Password.RequireDigit || options.Password.RequireLowercase || options.Password.RequireUppercase
            || options.Password.RequireNonAlphanumeric).ShouldBeFalse();
        options.User.RequireUniqueEmail.ShouldBeTrue();
        options.User.AllowedUserNameCharacters.ShouldBeEmpty();
    }

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
    public async Task CreateUser_EmailWithAnApostrophe_IsCreated()
    {
        // Arrange: a valid address Identity's default user-name alphabet would refuse; the user name is the email.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, $"o'brien-{id:N}@example.com");

        // Assert
        outcome.ShouldBe(CreateUserOutcome.Created);
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
    public async Task CreateUser_RoleThatDoesNotExist_ThrowsAndLeavesNoUser()
    {
        // Arrange: the user and its role row are one transaction, so a failed role leaves nothing behind.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();

        // Act
        Func<Task> act = () => CreateAsync(services, id, $"user-{id:N}@example.com", role: $"missing-{id:N}");

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        (await UserExistsAsync(services, id)).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateUser_WithARoleWhenTheFirstCommitIsLost_IsCreatedInThatRoleOnTheRetry()
    {
        // Arrange: the connection drops at the first commit; the retrying strategy runs the transaction again.
        FailFirstCommit failure = new();
        await using ServiceProvider services = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(failure));
        });
        await EnsureRoleAsync(services, Tester);
        var id = Guid.CreateVersion7();
        failure.Arm();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, $"user-{id:N}@example.com", role: Tester);

        // Assert
        failure.Fired.ShouldBeTrue();
        outcome.ShouldBe(CreateUserOutcome.Created);
        (await RolesAsync(services, id)).ShouldBe([Tester]);
    }

    [Fact]
    public async Task CreateUser_WithARoleWhenTheFirstCommitsAcknowledgementIsLost_IsCreatedOnce()
    {
        // Arrange: the commit reaches Postgres, its answer doesn't — the retry finds the user already there (ADR 0013).
        LoseFirstCommitAcknowledgement failure = new();
        await using ServiceProvider services = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(failure));
        });
        await EnsureRoleAsync(services, Tester);
        var id = Guid.CreateVersion7();
        failure.Arm();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, $"user-{id:N}@example.com", role: Tester);

        // Assert
        failure.Fired.ShouldBeTrue();
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
    public async Task CreateUser_SameEmailRegisteredMeanwhile_IsEmailTaken()
    {
        // Arrange: another registration of the address commits after Identity's own "taken?" check passed and before
        // this insert — the race only the database's unique index sees (ADR 0013).
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        BeforeNextSave race = new(async () =>
        {
            await using ServiceProvider other = postgres.CreateServices(AuthServices.Configure);
            (await CreateAsync(other, Guid.CreateVersion7(), email)).ShouldBe(CreateUserOutcome.Created);
        });
        await using ServiceProvider services = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race));
        });
        race.Arm();

        // Act
        CreateUserOutcome outcome = await CreateAsync(services, id, email);

        // Assert
        race.Fired.ShouldBeTrue();
        outcome.ShouldBe(CreateUserOutcome.EmailTaken);
        (await UserExistsAsync(services, id)).ShouldBeFalse();
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
        (await UserExistsAsync(services, id)).ShouldBeFalse();
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
        // Arrange: no account, so nothing real to verify against — the refusal still runs the hasher once, on a hash
        // of the same cost as a real one, so its timing says nothing about whether the email is registered (ADR 0031).
        CountingPasswordHasher hasher = new();
        await using ServiceProvider services = postgres.CreateServices(serviceCollection => Configure(serviceCollection, hasher));

        // Act
        UserAccount? account = await CheckAsync(services, $"nobody-{Guid.CreateVersion7():N}@example.com", AuthServices.Password);

        // Assert
        account.ShouldBeNull();
        hasher.VerifiedHashes.Count.ShouldBe(1);
        CountingPasswordHasher.IterationsOf(hasher.VerifiedHashes[0]).ShouldBe(new PasswordHasherOptions().IterationCount);
    }

    [Fact]
    public async Task CheckPassword_TooManyWrongAttempts_LocksOutAndTheRightPasswordFailsAtTheCostOfAHash()
    {
        // Arrange: every wrong attempt verifies the real hash; the locked-out attempt has nothing to verify and runs
        // the hasher anyway, at the same cost.
        CountingPasswordHasher hasher = new();
        await using ServiceProvider services = postgres.CreateServices(serviceCollection => Configure(serviceCollection, hasher));
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
        hasher.VerifiedHashes.Count.ShouldBe(IdentityService.MaxFailedAccessAttempts + 1);
        CountingPasswordHasher.IterationsOf(hasher.VerifiedHashes[^1]).ShouldBe(new PasswordHasherOptions().IterationCount);
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

    [Fact]
    public async Task CheckPassword_RightPasswordWhileAnotherRequestWritesTheUser_SignsInAndLeavesNothingStale()
    {
        // Arrange: a wrong attempt first, so the right one resets the count; just before that reset is saved another
        // request writes the row (a second submit), and the save loses on the concurrency stamp. The refresh token
        // saved next in the same scope must not replay the stale write.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);
        await CheckAsync(services, email, "wrong horse battery");
        BeforeNextSave race = new(() => ExecuteAsync("UPDATE users SET concurrency_stamp = @stamp WHERE id = @id", id));
        await using ServiceProvider racing = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race));
        });
        race.Arm();

        // Act
        await using AsyncServiceScope scope = racing.CreateAsyncScope();
        UserAccount? account = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .CheckPasswordAsync(email, AuthServices.Password, CancellationToken.None);
        RefreshToken token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateRefreshTokenAsync(id, CancellationToken.None);

        // Assert
        race.Fired.ShouldBeTrue();
        account.ShouldNotBeNull();
        token.Token.ShouldNotBeNullOrWhiteSpace();
        (await AccessFailedCountAsync(services, id)).ShouldBe(0);
    }

    [Fact]
    public async Task CheckPassword_WrongPasswordWhileAnotherGuessIsCounted_CountsBoth()
    {
        // Arrange: just before this wrong attempt's count is saved, another wrong attempt at the same account is
        // counted. Both must count, or guesses sent together would get far more tries than the lockout allows.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        var id = Guid.CreateVersion7();
        string email = $"user-{id:N}@example.com";
        await CreateAsync(services, id, email);
        BeforeNextSave race = new(() => ExecuteAsync(
            "UPDATE users SET access_failed_count = access_failed_count + 1, concurrency_stamp = @stamp WHERE id = @id", id));
        await using ServiceProvider racing = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race));
        });
        race.Arm();

        // Act
        UserAccount? account = await CheckAsync(racing, email, "wrong horse battery");

        // Assert
        race.Fired.ShouldBeTrue();
        account.ShouldBeNull();
        (await AccessFailedCountAsync(services, id)).ShouldBe(2);
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

    private static async Task<bool> UserExistsAsync(ServiceProvider services, Guid id)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(user => user.Id == id);
    }

    private static async Task<int> AccessFailedCountAsync(ServiceProvider services, Guid id)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Where(user => user.Id == id)
            .Select(user => user.AccessFailedCount)
            .SingleAsync();
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

    // A write by "another request": its own connection, committed at once, giving the row a new concurrency stamp.
    private async Task ExecuteAsync(string sql, Guid id)
    {
        await using NpgsqlConnection connection = new(postgres.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("stamp", Guid.CreateVersion7().ToString());
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    // The configured hasher, recording the hashes it verifies: the proof that a refusal costs a hash, of the same cost
    // as a real one, whether or not there is a real one to check.
    private sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> _inner = new();

        public List<string> VerifiedHashes { get; } = [];

        // A V3 hash: format byte 0x01, then the PRF and the iteration count as big-endian 32-bit numbers.
        public static int IterationsOf(string hash)
        {
            byte[] bytes = Convert.FromBase64String(hash);
            bytes[0].ShouldBe((byte)0x01);
            return (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));
        }

        public string HashPassword(ApplicationUser user, string password) => _inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            VerifiedHashes.Add(hashedPassword);
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    // Loses the answer to the first commit after Arm(): the transaction is committed, then the connection reports a
    // transient error, as a connection dropped between the commit and its acknowledgement does.
    private sealed class LoseFirstCommitAcknowledgement : DbTransactionInterceptor
    {
        private bool _armed;

        public bool Fired { get; private set; }

        public void Arm() => _armed = true;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                Fired = true;
                throw new NpgsqlException("Simulated lost acknowledgement of a commit.", new TimeoutException());
            }

            return Task.CompletedTask;
        }
    }

    // Loses the first commit after Arm() the way a dropped connection does: a transient Npgsql error, which the
    // retrying execution strategy answers by running the transaction again.
    private sealed class FailFirstCommit : DbTransactionInterceptor
    {
        private bool _armed;

        public bool Fired { get; private set; }

        public void Arm() => _armed = true;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                Fired = true;
                throw new NpgsqlException("Simulated lost connection at commit.", new TimeoutException());
            }

            return ValueTask.FromResult(result);
        }
    }
}
