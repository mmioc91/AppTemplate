using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AppTemplate.Api;
using AppTemplate.Api.Auth;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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
    public async Task AccessToken_ValidatedByTheHostsBearerHandler_GivesTheNameAndTheRoles()
    {
        // Arrange: the host's own handler and parameters, as a request is validated — so dropping RoleClaimType or
        // NameClaimType, or mapping inbound claims again, goes red here, not first in a project's 403s.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        await AuthClient.SignInWithRoleAsync(factory, client, Admin);
        string accessToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        JwtBearerOptions bearer = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        // Act
        TokenValidationResult result = await bearer.TokenHandlers[0].ValidateTokenAsync(accessToken, bearer.TokenValidationParameters);

        // Assert
        result.IsValid.ShouldBeTrue();
        ClaimsPrincipal principal = new(result.ClaimsIdentity);
        principal.Identity!.Name.ShouldBe("Rita");
        principal.IsInRole(Admin).ShouldBeTrue();
    }

    // Tokens minted with the host's signing key, each wrong in one way only; the valid one is the control row.
    public static TheoryData<string, string, bool, HttpStatusCode> MintedTokens => new()
    {
        { "AppTemplate", "AppTemplate", true, HttpStatusCode.OK },
        { "Someone", "AppTemplate", true, HttpStatusCode.Unauthorized },
        { "AppTemplate", "Someone", true, HttpStatusCode.Unauthorized },
        { "AppTemplate", "AppTemplate", false, HttpStatusCode.Unauthorized },
    };

    [Theory]
    [MemberData(nameof(MintedTokens))]
    public async Task Request_WithAMintedToken_IsAcceptedOnlyFromTheIssuerForTheAudienceAndSigned(
        string issuer, string audience, bool signed, HttpStatusCode expected)
    {
        // Arrange: a token for this Api's user, issued elsewhere (another issuer, another audience) or unsigned.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);
        AuthClient.UseBearer(client, Mint(new JsonWebToken(tokens.AccessToken).Subject, issuer, audience, signed));

        // Act
        HttpResponseMessage response = await client.GetAsync(AuthClient.Me);

        // Assert
        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Login_FromAnotherClient_HasAWindowOfItsOwn()
    {
        // Arrange: one client spends its window.
        await using ApiFactory factory = new(postgres);
        WebApplicationFactory<Program> clients = WithClientAddresses(factory);
        using HttpClient first = Client(clients, "203.0.113.10");
        using HttpClient second = Client(clients, "203.0.113.20");
        await SpendTheSignInWindowAsync(first);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(second, AuthClient.UniqueEmail());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_FromAnotherAddressOfTheSameIpv6Network_SharesItsWindow()
    {
        // Arrange: a host is given a whole /64 and can use a fresh address per request (ApiRateLimiting.ClientKey).
        await using ApiFactory factory = new(postgres);
        WebApplicationFactory<Program> clients = WithClientAddresses(factory);
        using HttpClient first = Client(clients, "2001:db8:1:2::10");
        using HttpClient second = Client(clients, "2001:db8:1:2::20");
        await SpendTheSignInWindowAsync(first);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(second, AuthClient.UniqueEmail());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_ThroughATrustedProxy_HasAWindowPerForwardedClient()
    {
        // Arrange: every request comes from the proxy, which names its client in X-Forwarded-For (ADR 0029).
        await using ApiFactory factory = new(postgres, settings: new Dictionary<string, string> { [ProxySetup.KnownNetworksKey] = "10.0.0.0/8" });
        WebApplicationFactory<Program> clients = WithClientAddresses(factory);
        using HttpClient first = Client(clients, "10.0.0.5", forwardedFor: "203.0.113.10");
        using HttpClient second = Client(clients, "10.0.0.5", forwardedFor: "203.0.113.20");
        await SpendTheSignInWindowAsync(first);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(second, AuthClient.UniqueEmail());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ForwardedByAnUntrustedPeer_CountsAgainstThatPeer()
    {
        // Arrange: a peer outside Proxy:KnownNetworks can't escape its window by naming another client.
        await using ApiFactory factory = new(postgres);
        WebApplicationFactory<Program> clients = WithClientAddresses(factory);
        using HttpClient first = Client(clients, "198.51.100.7", forwardedFor: "203.0.113.10");
        using HttpClient second = Client(clients, "198.51.100.7", forwardedFor: "203.0.113.20");
        await SpendTheSignInWindowAsync(first);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(second, AuthClient.UniqueEmail());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_OverTheRateLimit_Returns429WithACodeAndRetryAfter()
    {
        // Arrange: register and login share one window per client; the test server is one client.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();

        for (int attempt = 0; attempt < AuthRateLimitOptions.DefaultPerMinute; attempt++)
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
    public async Task Login_TheHostsLimit_IsTheWindow()
    {
        // Arrange: a host that raised or lowered the limit (RateLimits:AuthPerMinute).
        await using ApiFactory factory = new(postgres, settings: new Dictionary<string, string> { ["RateLimits:AuthPerMinute"] = "2" });
        using HttpClient client = factory.CreateClient();
        string email = AuthClient.UniqueEmail();
        (await AuthClient.LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AuthClient.LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Act
        HttpResponseMessage response = await AuthClient.LoginAsync(client, email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_AfterTheSignInWindowIsSpent_HasAWindowOfItsOwn()
    {
        // Arrange: the sign-in window used up — by one registration and failed logins.
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        AuthClient.TokenPair tokens = await AuthClient.RegisterAsync(client);

        for (int attempt = 1; attempt < AuthRateLimitOptions.DefaultPerMinute; attempt++)
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
            (await AuthClient.RefreshAsync(client, "not-a-token")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
        for (int call = 0; call < AuthRateLimitOptions.DefaultPerMinute + 1; call++)
        {
            statuses.Add((await client.GetAsync(AuthClient.Me)).StatusCode);
        }

        // Assert
        statuses.ShouldAllBe(status => status == HttpStatusCode.OK);
    }

    private static async Task SpendTheSignInWindowAsync(HttpClient client)
    {
        for (int attempt = 0; attempt < AuthRateLimitOptions.DefaultPerMinute; attempt++)
        {
            (await AuthClient.LoginAsync(client, AuthClient.UniqueEmail())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await AuthClient.LoginAsync(client, AuthClient.UniqueEmail())).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // The test server reports no client address; this host takes it from a test header, ahead of the whole pipeline —
    // where a socket would have set it, before the forwarded headers are read.
    private static WebApplicationFactory<Program> WithClientAddresses(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ClientAddressFromHeader>()));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string address, string? forwardedFor = null)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientAddressFromHeader.Header, address);
        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        }

        return client;
    }

    private static string Mint(string subject, string issuer, string audience, bool signed)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Subject = new ClaimsIdentity([new Claim(JwtClaims.Subject, subject), new Claim(JwtClaims.Name, "Ana")]),
            SigningCredentials = signed
                ? new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(ApiFactory.SigningKey)), SecurityAlgorithms.HmacSha256)
                : null,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
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

    private sealed class ClientAddressFromHeader : IStartupFilter
    {
        public const string Header = "X-Test-Client-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, rest) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out StringValues address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                }

                return rest(context);
            });
            next(app);
        };
    }
}
