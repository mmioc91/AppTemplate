using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AppTemplate.Api.Auth;
using AppTemplate.Api.Errors;
using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Common;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Persistence;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AuthEndpointsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_NewEmail_Returns201WithATokenPair()
    {
        // Arrange
        await using ApiFactory factory = new(postgres, new FakeTimeProvider(Start));
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(AuthClient.Register, new RegisterRequest(AuthClient.UniqueEmail(), AuthClient.Password, "Ana"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        AuthClient.TokenPair tokens = await AuthClient.ReadTokensAsync(response);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokens.AccessTokenExpiresAt.ShouldBe(Start.AddMinutes(15));
        tokens.RefreshTokenExpiresAt.ShouldBe(Start.AddDays(14));
    }

    [Fact]
    public async Task Register_ThenMe_ReturnsTheNewAccountWithoutRoles()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client, email, "  Ana  ");
        AuthClient.UseBearer(client, tokens.AccessToken);

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetGuid().Version.ShouldBe(7);
        body.RootElement.GetProperty("email").GetString().ShouldBe(email);
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("Ana");
        body.RootElement.GetProperty("roles").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Register_EmailWithAnApostrophe_Returns201()
    {
        // Arrange: a valid address Identity's default user-name alphabet would refuse (ADR 0031).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthClient.Register,
            new RegisterRequest($"o'brien-{Guid.CreateVersion7():N}@example.com", AuthClient.Password, "Seán"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Login_EmailWithANulCharacter_Returns400OnTheEmailField()
    {
        // Arrange: Postgres refuses a NUL in a text parameter; the validator answers before the lookup.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, "ana\u0000@example.com");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").GetProperty("email").EnumerateArray().Select(code => code.GetString())
            .ShouldBe([ValidationCodes.Invalid]);
    }

    [Fact]
    public async Task Register_EmailAlreadyRegistered_Returns409()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        await AuthClient.RegisterAsync(client, email);

        // Act: Identity compares emails case-insensitively, so a different casing is the same account.
        HttpResponseMessage response = await client.PostAsJsonAsync(AuthClient.Register, new RegisterRequest(email.ToUpperInvariant(), AuthClient.Password, "Ana"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.EmailTakenCode);
    }

    [Fact]
    public async Task Register_PasswordUnderMinLength_Returns400OnThePasswordField()
    {
        // Arrange: the validator reports it with the other field errors; Identity's own rule is the backstop (ADR 0031).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthClient.Register,
            new RegisterRequest(AuthClient.UniqueEmail(), new string('p', AuthRules.PasswordMinLength - 1), "Ana"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(Error.ValidationFailedCode);
        body.RootElement.GetProperty("errors").GetProperty("password").EnumerateArray().Select(code => code.GetString())
            .ShouldBe([ValidationCodes.TooShort]);
    }

    [Fact]
    public async Task Register_EmptyBody_Returns400WithEveryMissingField()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(AuthClient.Register, new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(Error.ValidationFailedCode);
        body.RootElement.GetProperty("errors").EnumerateObject().Select(field => field.Name)
            .ShouldBe(["email", "password", "displayName"], ignoreOrder: true);
    }

    [Fact]
    public async Task Register_MalformedJson_Returns400MalformedRequest()
    {
        // Arrange: the first endpoint with a body proves the promise from ADR 0018.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        using StringContent body = new("{\"email\": ", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await client.PostAsync(AuthClient.Register, body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(ProblemCodes.MalformedRequest);
    }

    [Fact]
    public async Task Register_UnhandledException_Returns500WithACodeAndNoDetails()
    {
        // Arrange: a port that blows up stands in for any bug behind an endpoint (ADR 0018).
        await using ApiFactory factory = new(postgres);
        ITokenService broken = Substitute.For<ITokenService>();
        broken.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<IReadOnlyList<string>>()).Throws(new InvalidOperationException("secret detail"));
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddScoped(_ => broken)))
            .CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(AuthClient.Register, new RegisterRequest(AuthClient.UniqueEmail(), AuthClient.Password, "Ana"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        string json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        body.RootElement.GetProperty("code").GetString().ShouldBe(ProblemCodes.UnexpectedError);
        body.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();
        json.ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task Register_StoresOnlyTheHashOfTheRefreshToken()
    {
        // Arrange: a whole-second instant, since Postgres keeps microseconds.
        await using ApiFactory factory = new(postgres, new FakeTimeProvider(Start));
        using HttpClient client = factory.CreateClient();

        // Act
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);

        // Assert
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RefreshTokens.AnyAsync(token => token.TokenHash == tokens.RefreshToken)).ShouldBeFalse();
        StoredRefreshToken stored = await db.RefreshTokens.SingleAsync(token => token.TokenHash == TokenService.Hash(tokens.RefreshToken));
        stored.RevokedAt.ShouldBeNull();
        stored.ExpiresAt.ShouldBe(Start.AddDays(14));
    }

    [Fact]
    public async Task Login_RightPassword_Returns200WithATokenPair()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        await AuthClient.RegisterAsync(client, email);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthClient.TokenPair tokens = await AuthClient.ReadTokensAsync(response);
        AuthClient.UseBearer(client, tokens.AccessToken);
        (await client.GetAsync(AuthClient.Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        await AuthClient.RegisterAsync(client, email);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, email, "wrong horse battery");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidCredentialsCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithTheSameCodeAsAWrongPassword()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, AuthClient.UniqueEmail());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidCredentialsCode);
    }

    [Fact]
    public async Task Login_AfterTooManyWrongPasswords_RejectsTheRightOneWithTheSameCode()
    {
        // Arrange: the lockout answers exactly like a wrong password, so nobody learns it happened (ADR 0031).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        await AuthClient.RegisterAsync(client, email);

        for (int attempt = 0; attempt < IdentityService.MaxFailedAccessAttempts; attempt++)
        {
            (await AuthClient.LoginAsync(client, email, "wrong horse battery")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidCredentialsCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesItAndTheOldOneStopsWorkingOnceTheNewOneIsUsed()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair first = await AuthClient.RegisterAsync(client);

        // Act
        HttpResponseMessage rotated = await AuthClient.RefreshAsync(client, first.RefreshToken);
        AuthClient.TokenPair second = await AuthClient.ReadTokensAsync(rotated);
        (await AuthClient.RefreshAsync(client, second.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage replayed = await AuthClient.RefreshAsync(client, first.RefreshToken);

        // Assert
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        second.AccessToken.ShouldNotBe(first.AccessToken);
        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(replayed)).ShouldBe(AuthErrors.InvalidRefreshTokenCode);
    }

    [Fact]
    public async Task Refresh_SpentTokenAgainBeforeItsReplacementWasUsed_IssuesANewPair()
    {
        // Arrange: the answer to the first refresh never reached the page — it was reloaded (ADR 0031). A fixed clock,
        // so the grace after the rotation cannot run out on a slow machine.
        await using ApiFactory factory = new(postgres, new FakeTimeProvider(Start));
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair first = await AuthClient.RegisterAsync(client);
        (await AuthClient.RefreshAsync(client, first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage again = await AuthClient.RefreshAsync(client, first.RefreshToken);

        // Assert: a working pair, the session alive.
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthClient.TokenPair kept = await AuthClient.ReadTokensAsync(again);
        (await AuthClient.RefreshAsync(client, kept.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesTheNewestTokenToo()
    {
        // Arrange: a token that comes back after its replacement was used means two parties hold it; the whole family
        // dies.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair first = await AuthClient.RegisterAsync(client);
        AuthClient.TokenPair second = await AuthClient.ReadTokensAsync(await AuthClient.RefreshAsync(client, first.RefreshToken));
        AuthClient.TokenPair third = await AuthClient.ReadTokensAsync(await AuthClient.RefreshAsync(client, second.RefreshToken));
        (await AuthClient.RefreshAsync(client, first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Act
        HttpResponseMessage response = await AuthClient.RefreshAsync(client, third.RefreshToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidRefreshTokenCode);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        // Arrange
        FakeTimeProvider time = new(Start);
        await using ApiFactory factory = new(postgres, time);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        time.Advance(TimeSpan.FromDays(14));

        // Act
        HttpResponseMessage response = await AuthClient.RefreshAsync(client, tokens.RefreshToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidRefreshTokenCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await AuthClient.RefreshAsync(client, "never-issued");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.InvalidRefreshTokenCode);
    }

    [Fact]
    public async Task Logout_ThenRefresh_Returns401()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, tokens.AccessToken);

        // Act
        HttpResponseMessage logout = await client.PostAsJsonAsync(AuthClient.Logout, new LogoutRequest(tokens.RefreshToken));
        HttpResponseMessage refresh = await AuthClient.RefreshAsync(client, tokens.RefreshToken);

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(refresh)).ShouldBe(AuthErrors.InvalidRefreshTokenCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logout_TokenAlreadyRevokedOrNeverIssued_StillReturns204(bool alreadyRevoked)
    {
        // Arrange: a logout that already happened is not an error (ADR 0031).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, tokens.AccessToken);
        string presented = alreadyRevoked ? tokens.RefreshToken : "never-issued";
        if (alreadyRevoked)
        {
            (await client.PostAsJsonAsync(AuthClient.Logout, new LogoutRequest(presented))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(AuthClient.Logout, new LogoutRequest(presented));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithAnotherUsersRefreshToken_LeavesItWorking()
    {
        // Arrange: the token in the body is only revoked for the user the access token belongs to.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair victim = await AuthClient.RegisterAsync(client);
        AuthClient.TokenPair attacker = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, attacker.AccessToken);

        // Act
        HttpResponseMessage logout = await client.PostAsJsonAsync(AuthClient.Logout, new LogoutRequest(victim.RefreshToken));

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthClient.RefreshAsync(client, victim.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_UserDeletedAfterTheTokenWasIssued_Returns404()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client, email);
        AuthClient.UseBearer(client, tokens.AccessToken);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Remove(await db.Users.SingleAsync(user => user.Email == email));
            await db.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(AuthErrors.UserNotFoundCode);
    }
}
