using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Auth;

// The refresh-token store under concurrency, on real rows with real xmin (ADR 0031, 0013).
[Collection(PostgresCollection.Name)]
public sealed class TokenServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RotateRefreshToken_TwoScopesRotateTheSameTokenWithinTheGrace_TheLoserIsAnsweredLikeAReloadedPage()
    {
        // Arrange: the stale scope loads the token before the other one rotates it (xmin changes); its own rotation
        // then updates a row that moved on, so it reads the family again and the reuse rules decide (ADR 0031).
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);

        await using AsyncServiceScope stale = services.CreateAsyncScope();
        await LoadFamilyAsync(stale, raw);
        RefreshTokenRotation first = (await RotateAsync(services, raw)).ShouldNotBeNull();

        // Act
        RefreshTokenRotation? second = await stale.ServiceProvider.GetRequiredService<ITokenService>()
            .RotateRefreshTokenAsync(raw, CancellationToken.None);

        // Assert: within the grace and with the winner's replacement unused, that replacement is retired and the loser
        // gets its successor — one live token in the family, as after a page reloaded mid-refresh.
        second.ShouldNotBeNull().Replacement.Token.ShouldNotBe(first.Replacement.Token);
        (await LiveTokensInFamilyOfAsync(services, raw)).ShouldBe(1);
        (await RotateAsync(services, second.Replacement.Token)).ShouldNotBeNull();
    }

    [Fact]
    public async Task RotateRefreshToken_LoserOfARaceArrivingPastTheGrace_RevokesTheFamily()
    {
        // Arrange: a copy of the token raced the rightful rotation and lost on xmin; read again, the token is long spent.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);

        await using AsyncServiceScope stale = services.CreateAsyncScope();
        await LoadFamilyAsync(stale, raw);
        RefreshTokenRotation first = (await RotateAsync(services, raw)).ShouldNotBeNull();
        Clock(services).Advance(TokenService.ReuseGrace + TimeSpan.FromSeconds(1));

        // Act
        RefreshTokenRotation? second = await stale.ServiceProvider.GetRequiredService<ITokenService>()
            .RotateRefreshTokenAsync(raw, CancellationToken.None);

        // Assert: theft detected — nothing for the loser, and the winner's chain is dead too.
        second.ShouldBeNull();
        (await LiveTokensInFamilyOfAsync(services, raw)).ShouldBe(0);
        (await RotateAsync(services, first.Replacement.Token)).ShouldBeNull();
    }

    [Fact]
    public async Task RevokeRefreshToken_WhileARefreshRotatesATokenOfTheFamily_RevokesTheReplacementToo()
    {
        // Arrange: the competing rotation runs inside the revoke's window — after it listed the live tokens, before
        // it saved — so its first save loses on xmin and it must go again.
        string raw = string.Empty;
        CompetingWrite race = new(() => RotateAsync(postgres.CreateServices(AuthServices.Configure), raw));
        await using ServiceProvider services = postgres.CreateServices(serviceCollection =>
        {
            AuthServices.Configure(serviceCollection);
            serviceCollection.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race));
        });
        Guid userId = await AuthServices.CreateUserAsync(services);
        raw = await CreateTokenAsync(services, userId);
        race.Arm();

        // Act
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ITokenService>().RevokeRefreshTokenAsync(raw, userId, CancellationToken.None);

        // Assert: the rotation happened in between, and nothing in the family is live any more.
        race.Fired.ShouldBeTrue();
        race.Rotation.ShouldNotBeNull();
        (await LiveTokensInFamilyOfAsync(services, raw)).ShouldBe(0);
        (await RotateAsync(services, race.Rotation.Replacement.Token)).ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public async Task RotateRefreshToken_SpentTokenWithinTheGraceItsReplacementUnused_IssuesANewOne(int secondsLater)
    {
        // Arrange: a page reloaded before it stored the pair the first rotation gave it (ADR 0031).
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);
        RefreshTokenRotation lost = (await RotateAsync(services, raw)).ShouldNotBeNull();
        Clock(services).Advance(TimeSpan.FromSeconds(secondsLater));

        // Act
        RefreshTokenRotation? again = await RotateAsync(services, raw);

        // Assert: a new token of the same family; the one never delivered is retired.
        again.ShouldNotBeNull().Replacement.Token.ShouldNotBe(lost.Replacement.Token);
        (await LiveTokensInFamilyOfAsync(services, raw)).ShouldBe(1);
        (await RotateAsync(services, again.Replacement.Token)).ShouldNotBeNull();
    }

    [Fact]
    public async Task RotateRefreshToken_SpentTokenPastTheGrace_RevokesTheFamily()
    {
        // Arrange
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);
        RefreshTokenRotation first = (await RotateAsync(services, raw)).ShouldNotBeNull();
        Clock(services).Advance(TokenService.ReuseGrace + TimeSpan.FromSeconds(1));

        // Act
        RefreshTokenRotation? again = await RotateAsync(services, raw);

        // Assert
        again.ShouldBeNull();
        (await RotateAsync(services, first.Replacement.Token)).ShouldBeNull();
    }

    [Fact]
    public async Task RotateRefreshToken_SpentTokenAfterItsReplacementWasUsed_RevokesTheFamily()
    {
        // Arrange: the replacement reached its holder and was used — the spent token is someone else's copy.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);
        RefreshTokenRotation first = (await RotateAsync(services, raw)).ShouldNotBeNull();
        RefreshTokenRotation second = (await RotateAsync(services, first.Replacement.Token)).ShouldNotBeNull();

        // Act
        RefreshTokenRotation? replayed = await RotateAsync(services, raw);

        // Assert
        replayed.ShouldBeNull();
        (await RotateAsync(services, second.Replacement.Token)).ShouldBeNull();
    }

    [Fact]
    public async Task RotateRefreshToken_SignedOutTokenWithinTheGrace_IsRefused()
    {
        // Arrange: a sign-out revokes without a replacement — nothing was lost in transit.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string raw = await CreateTokenAsync(services, userId);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITokenService>().RevokeRefreshTokenAsync(raw, userId, CancellationToken.None);
        }

        // Act
        RefreshTokenRotation? again = await RotateAsync(services, raw);

        // Assert
        again.ShouldBeNull();
    }

    [Fact]
    public async Task RevokeRefreshToken_ForTheFamily_LeavesOtherFamiliesOfTheUserAlone()
    {
        // Arrange: two sign-ins, two families.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string phone = await CreateTokenAsync(services, userId);
        string laptop = await CreateTokenAsync(services, userId);

        // Act
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ITokenService>().RevokeRefreshTokenAsync(phone, userId, CancellationToken.None);

        // Assert
        (await RotateAsync(services, phone)).ShouldBeNull();
        (await RotateAsync(services, laptop)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateRefreshToken_SignIn_DeletesTheUsersExpiredTokensOnly()
    {
        // Arrange: a token of this user and one of another user, both past their expiry.
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        Guid otherUserId = await AuthServices.CreateUserAsync(services);
        string expired = await CreateTokenAsync(services, userId);
        string othersExpired = await CreateTokenAsync(services, otherUserId);
        Clock(services).Advance(TimeSpan.FromDays(15));

        // Act
        string fresh = await CreateTokenAsync(services, userId);

        // Assert
        (await RowExistsAsync(services, expired)).ShouldBeFalse();
        (await RowExistsAsync(services, fresh)).ShouldBeTrue();
        (await RowExistsAsync(services, othersExpired)).ShouldBeTrue();
    }

    [Fact]
    public async Task RotateRefreshToken_DeletesTheFamilysExpiredTokensAndKeepsTheSpentOnesStillInDate()
    {
        // Arrange: T1 rotated a day in, T2 an hour after T1 expired and a day before its own expiry. A spent token
        // still in date is kept, so a copy of it presented later is still caught as a reuse (ADR 0031).
        await using ServiceProvider services = postgres.CreateServices(AuthServices.Configure);
        Guid userId = await AuthServices.CreateUserAsync(services);
        string t1 = await CreateTokenAsync(services, userId);
        Clock(services).Advance(TimeSpan.FromDays(1));
        RefreshTokenRotation second = (await RotateAsync(services, t1)).ShouldNotBeNull();
        Clock(services).Advance(TimeSpan.FromDays(13) + TimeSpan.FromHours(1));

        // Act
        RefreshTokenRotation third = (await RotateAsync(services, second.Replacement.Token)).ShouldNotBeNull();

        // Assert
        (await RowExistsAsync(services, t1)).ShouldBeFalse();
        (await RowExistsAsync(services, second.Replacement.Token)).ShouldBeTrue();
        (await RowExistsAsync(services, third.Replacement.Token)).ShouldBeTrue();
    }

    private static FakeTimeProvider Clock(ServiceProvider services) => (FakeTimeProvider)services.GetRequiredService<TimeProvider>();

    private static async Task<string> CreateTokenAsync(ServiceProvider services, Guid userId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        RefreshToken token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateRefreshTokenAsync(userId, CancellationToken.None);
        return token.Token;
    }

    private static async Task<RefreshTokenRotation?> RotateAsync(ServiceProvider services, string raw)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITokenService>().RotateRefreshTokenAsync(raw, CancellationToken.None);
    }

    // Tracks the token's family in the scope's context, so a later query there returns these (soon stale) instances.
    private static async Task LoadFamilyAsync(AsyncServiceScope scope, string raw)
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string hash = TokenService.Hash(raw);
        Guid familyId = await db.RefreshTokens.Where(token => token.TokenHash == hash).Select(token => token.FamilyId).SingleAsync();
        await db.RefreshTokens.Where(token => token.FamilyId == familyId).ToListAsync();
    }

    private static async Task<bool> RowExistsAsync(ServiceProvider services, string raw)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        string hash = TokenService.Hash(raw);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.AnyAsync(token => token.TokenHash == hash);
    }

    private static async Task<int> LiveTokensInFamilyOfAsync(ServiceProvider services, string raw)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string hash = TokenService.Hash(raw);
        Guid familyId = await db.RefreshTokens.Where(token => token.TokenHash == hash).Select(token => token.FamilyId).SingleAsync();
        return await db.RefreshTokens.CountAsync(token => token.FamilyId == familyId && token.RevokedAt == null);
    }

    // Runs one competing write right before the first save after Arm() — inside the window between a method's query
    // and its SaveChanges. Disarms itself first, so the competing write's own save doesn't fire it again.
    private sealed class CompetingWrite(Func<Task<RefreshTokenRotation?>> write) : SaveChangesInterceptor
    {
        private bool _armed;

        public bool Fired { get; private set; }

        public RefreshTokenRotation? Rotation { get; private set; }

        public void Arm() => _armed = true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                Fired = true;
                Rotation = await write();
            }

            return result;
        }
    }
}
