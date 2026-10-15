using System.Net;
using System.Text.Json;
using AppTemplate.Api.Auth;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api.Auth;

// Host-wide authentication behavior (ADR 0031): the fallback policy, token validation, the claims and the rate
// limits — independent of any one endpoint.
[Collection(PostgresCollection.Name)]
public sealed class AuthenticationTests(PostgresFixture postgres)
{
    private const string Admin = "Admin";

    private static readonly DateTimeOffset Start = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_WithoutAToken_Returns401Unauthenticated()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(ProblemCodes.Unauthenticated);
    }

    [Fact]
    public async Task Request_WithABadSignature_Returns401()
    {
        // Arrange: the same token with its signature altered.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        string[] parts = tokens.AccessToken.Split('.');
        parts[2] = new string(parts[2].Reverse().ToArray());
        AuthClient.UseBearer(client, string.Join('.', parts));

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(ProblemCodes.Unauthenticated);
    }

    [Fact]
    public async Task Request_WithAnExpiredAccessToken_Returns401()
    {
        // Arrange: 15 minutes of lifetime plus the 30 seconds of tolerated clock skew.
        FakeTimeProvider time = new(Start);
        await using ApiFactory factory = new(postgres, time);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, tokens.AccessToken);
        time.Advance(TimeSpan.FromMinutes(16));

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(ProblemCodes.Unauthenticated);
    }

    [Fact]
    public async Task Request_WithAnAccessTokenInsideTheClockSkew_IsAccepted()
    {
        // Arrange
        FakeTimeProvider time = new(Start);
        await using ApiFactory factory = new(postgres, time);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, tokens.AccessToken);
        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(20));

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EveryEndpointNotMarkedAnonymous_RejectsAnUnauthenticatedCall()
    {
        // Arrange: walks the real endpoint table, so an endpoint added later can't forget the fallback policy.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        List<(string Method, string Path)> protectedEndpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(endpoint => endpoint.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods
                .Select(method => (method, ConcreteUrl(endpoint.RoutePattern))))
            .ToList();
        protectedEndpoints.ShouldContain(("POST", "/api/v1/auth/logout"));
        protectedEndpoints.ShouldContain(("GET", "/api/v1/auth/me"));

        // Act
        List<(string Method, string Path, HttpStatusCode Status)> answers = [];
        foreach ((string method, string path) in protectedEndpoints)
        {
            using HttpRequestMessage request = new(new HttpMethod(method), new Uri(path, UriKind.Relative));
            HttpResponseMessage response = await client.SendAsync(request);
            answers.Add((method, path, response.StatusCode));
        }

        // Assert
        answers.ShouldAllBe(answer => answer.Status == HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(HealthEndpoints.Live)]
    [InlineData(HealthEndpoints.Ready)]
    [InlineData("/openapi/v1.json")]
    public async Task OperationalEndpoints_WithoutAToken_StayAnonymous(string path)
    {
        // Arrange: probes and the OpenAPI document (Development) are the deliberate exceptions to the fallback policy.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AccessToken_CarriesTheSubjectEmailNameAndRoles()
    {
        // Arrange: the claims the Api validates against (JwtClaims); the role as the bearer identity's role claim type,
        // so a policy's RequireRole and ICurrentUser.IsInRole read it.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        await AuthClient.SignInWithRoleAsync(factory, client, Admin);
        string accessToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        using var me = JsonDocument.Parse(await (await client.GetAsync(AuthClient.Me)).Content.ReadAsStringAsync());

        // Act
        JsonWebToken token = new(accessToken);

        // Assert
        token.Subject.ShouldBe(me.RootElement.GetProperty("id").GetGuid().ToString());
        token.GetClaim(JwtClaims.Email).Value.ShouldBe(me.RootElement.GetProperty("email").GetString());
        token.GetClaim(JwtClaims.Name).Value.ShouldBe("Rita");
        token.Claims.Where(claim => claim.Type == JwtClaims.Role).Select(claim => claim.Value).ShouldBe([Admin]);
        me.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe([Admin]);
    }

    [Fact]
    public async Task Login_OverTheRateLimit_Returns429WithACodeAndRetryAfter()
    {
        // Arrange: register and login share one window per client; the test server is one client.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();

        for (int attempt = 0; attempt < AuthRateLimiting.PermitLimit; attempt++)
        {
            (await AuthClient.LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.ShouldNotBeNull();
        (await AuthClient.ReadCodeAsync(response)).ShouldBe(ProblemCodes.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_AfterTheSignInWindowIsSpent_HasAWindowOfItsOwn()
    {
        // Arrange: the sign-in window used up — by one registration and failed logins.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);

        for (int attempt = 1; attempt < AuthRateLimiting.PermitLimit; attempt++)
        {
            await AuthClient.LoginAsync(client, AuthClient.UniqueEmail());
        }

        (await AuthClient.LoginAsync(client, AuthClient.UniqueEmail())).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Act
        HttpResponseMessage response = await AuthClient.RefreshAsync(client, tokens.RefreshToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_OverItsRateLimit_Returns429()
    {
        // Arrange: unknown tokens answer 401, and count all the same.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < AuthRateLimiting.RefreshPermitLimit; attempt++)
        {
            await AuthClient.RefreshAsync(client, "not-a-token");
        }

        // Act
        HttpResponseMessage response = await AuthClient.RefreshAsync(client, "not-a-token");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Me_IsNotRateLimited()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        await AuthClient.SignInAsync(client);

        // Act
        List<HttpStatusCode> statuses = [];
        for (int call = 0; call < AuthRateLimiting.PermitLimit + 1; call++)
        {
            statuses.Add((await client.GetAsync(AuthClient.Me)).StatusCode);
        }

        // Assert
        statuses.ShouldAllBe(status => status == HttpStatusCode.OK);
    }

    // A route pattern with its parameters filled: the version, and a fresh id for anything else.
    private static string ConcreteUrl(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart { Name: "version" } => "1",
            RoutePatternParameterPart => Guid.CreateVersion7().ToString(),
            _ => throw new InvalidOperationException($"Unexpected route part {part.GetType().Name}."),
        }))));
}
