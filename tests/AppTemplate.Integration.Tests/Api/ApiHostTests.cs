using System.Net;
using System.Text.Json;
using AppTemplate.Api;
using AppTemplate.Api.Auth;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AppTemplate.Integration.Tests.Api;

[Collection(PostgresCollection.Name)]
public sealed class ApiHostTests(PostgresFixture postgres)
{
    public enum Shade
    {
        Light,
        Dark,
    }

    [Theory]
    [InlineData(HealthEndpoints.Live)]
    [InlineData(HealthEndpoints.Ready)]
    public async Task Health_WithMigratedDatabase_IsHealthy(string path)
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_UnmigratedDatabaseInProduction_LiveIs200ButReadyIs503()
    {
        // Arrange: production never migrates on startup, so a deploy that skipped the migration step must stay out of
        // rotation without being restarted (ADR 0019, 0029).
        await using ApiFactory factory = new(await postgres.CreateEmptyDatabaseAsync(), Environments.Production);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage live = await client.GetAsync(new Uri(HealthEndpoints.Live, UriKind.Relative));
        HttpResponseMessage ready = await client.GetAsync(new Uri(HealthEndpoints.Ready, UriKind.Relative));

        // Assert
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Factory_WithATimeProvider_ReplacesTheHostsClock()
    {
        // Arrange: the factory registers the clock after Program.cs, so it beats AddApplication's TryAddSingleton.
        FakeTimeProvider fake = new();
        await using ApiFactory factory = new(postgres, fake);

        // Act
        TimeProvider time = factory.Services.GetRequiredService<TimeProvider>();

        // Assert
        time.ShouldBeSameAs(fake);
    }

    [Fact]
    public async Task Factory_WithSettings_MakesThemConfiguration()
    {
        // Arrange
        await using ApiFactory factory = new(postgres, settings: new Dictionary<string, string> { ["Probe:Key"] = "value" });

        // Act
        string? value = factory.Services.GetRequiredService<IConfiguration>()["Probe:Key"];

        // Assert
        value.ShouldBe("value");
    }

    [Fact]
    public async Task Problem_RequestWithATraceparent_CarriesThatTraceId()
    {
        // Arrange: a caller (a proxy, the web app) that started the trace (ADR 0028).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/nothing-here");
        request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty(ProblemDetailsSetup.TraceIdKey).GetString().ShouldBe("4bf92f3577b34da6a3ce929d0e0e4736");
    }

    [Fact]
    public async Task ForwardedHeaders_KnownNetworksConfigured_TrustsThemNextToLoopback()
    {
        // Arrange: the reverse proxy's network (ADR 0029); loopback stays trusted, as ASP.NET Core does by default.
        await using ApiFactory factory = new(postgres, settings: new Dictionary<string, string> { [ProxySetup.KnownNetworksKey] = "172.20.0.0/16, 10.0.0.0/8" });

        // Act
        ForwardedHeadersOptions options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.KnownIPNetworks.Select(network => network.ToString()).ShouldBe(["127.0.0.0/8", "172.20.0.0/16", "10.0.0.0/8"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("/nothing-here")]
    [InlineData("/api/v1/nothing-here")]
    public async Task UnknownRoute_ReturnsProblemDetailsWithCode(string path)
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        // Assert: the framework's own errors carry a code too (ADR 0018).
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(ProblemCodes.NotFound);
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_IsServedForV1()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.TryGetProperty("openapi", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("GET", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/me")]
    public async Task WrongMethod_WithoutAToken_Answers405NotA401(string method, string path)
    {
        // Arrange: the fallback policy would otherwise hide a wrong method behind a sign-in (ADR 0031).
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), new Uri(path, UriKind.Relative));

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(ProblemCodes.MethodNotAllowed);
    }

    [Fact]
    public async Task WrongContentType_WithoutAToken_Answers415NotA401()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();
        using StringContent content = new("{}", System.Text.Encoding.UTF8, "text/plain");

        // Act
        HttpResponseMessage response = await client.PostAsync(new Uri("/api/v1/auth/login", UriKind.Relative), content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(ProblemCodes.UnsupportedMediaType);
    }

    public static TheoryData<string> UnusableSigningKeys => new()
    {
        string.Empty,
        "not base64!",
        Convert.ToBase64String(new byte[JwtOptions.MinSigningKeyBytes - 1]),
    };

    [Theory]
    [MemberData(nameof(UnusableSigningKeys))]
    public async Task Startup_UnusableSigningKey_FailsNamingTheSetting(string key)
    {
        // Arrange: a host without a usable key must not start (ADR 0031) — not serve, and fail on every request.
        await using ApiFactory factory = new(postgres, settings: new Dictionary<string, string> { ["Jwt:SigningKey"] = key });

        // Act
        Action act = () => factory.CreateClient();

        // Assert
        OptionsValidationException exception = Should.Throw<OptionsValidationException>(act);
        exception.OptionsType.ShouldBe(typeof(JwtOptions));
        exception.Message.ShouldContain(nameof(JwtOptions.SigningKey));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Startup_SignInRateLimitBelowOne_FailsNamingTheSetting(string perMinute)
    {
        // Arrange: zero would switch signing in off; the host refuses it rather than start without a way in (ADR 0032).
        await using ApiFactory factory = new(
            postgres,
            settings: new Dictionary<string, string> { [$"{AuthRateLimitOptions.SectionName}:{nameof(AuthRateLimitOptions.AuthPerMinute)}"] = perMinute });

        // Act
        Action act = () => factory.CreateClient();

        // Assert
        OptionsValidationException exception = Should.Throw<OptionsValidationException>(act);
        exception.OptionsType.ShouldBe(typeof(AuthRateLimitOptions));
        exception.Message.ShouldContain(nameof(AuthRateLimitOptions.AuthPerMinute));
    }

    // Every endpoint declares exactly the statuses a client can meet (ADR 0009): one row per endpoint, added with it.
    [Theory]
    [InlineData("/api/v1/auth/register", "post", new[] { "201", "400", "409", "429" })]
    [InlineData("/api/v1/auth/login", "post", new[] { "200", "400", "401", "429" })]
    [InlineData("/api/v1/auth/refresh", "post", new[] { "200", "400", "401", "429" })]
    [InlineData("/api/v1/auth/logout", "post", new[] { "204", "400", "401" })]
    [InlineData("/api/v1/auth/me", "get", new[] { "200", "401", "404" })]
    public async Task OpenApiDocument_Endpoint_DeclaresExactlyTheseStatuses(string path, string method, string[] statuses)
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        using HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        // Assert
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).ShouldBe(statuses, ignoreOrder: true);
    }

    [Fact]
    public async Task HttpJson_UsesCamelCase_AsValidationFieldNamesAssume()
    {
        // Arrange: ErrorResults converts field names with camelCase; this fails if the Api's JSON naming changes.
        await using ApiFactory factory = new(postgres);

        // Act
        JsonSerializerOptions json = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Assert
        json.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public async Task HttpJson_EnumByName_IsAccepted()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        Shade shade = JsonSerializer.Deserialize<Shade>("\"Dark\"", options);

        // Assert
        shade.ShouldBe(Shade.Dark);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("99")]
    public async Task HttpJson_EnumAsNumber_IsRejected(string json)
    {
        // Arrange: a number would otherwise bind, even one that is no defined value (ADR 0020).
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        Action act = () => JsonSerializer.Deserialize<Shade>(json, options);

        // Assert
        Should.Throw<JsonException>(act);
    }

    [Theory]
    [InlineData("\"2026-06-14T10:00:00Z\"", 0)]
    [InlineData("\"2026-06-14T12:00:00+02:00\"", 2)]
    public async Task HttpJson_InstantWithAnOffset_IsThatInstant(string json, int offsetHours)
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        DateTimeOffset? instant = JsonSerializer.Deserialize<DateTimeOffset?>(json, options);

        // Assert
        instant.ShouldBe(new DateTimeOffset(2026, 6, 14, 10 + offsetHours, 0, 0, TimeSpan.FromHours(offsetHours)));
        instant!.Value.Offset.ShouldBe(TimeSpan.FromHours(offsetHours));
    }

    [Theory]
    [InlineData("\"2026-06-14T10:00:00\"")]
    [InlineData("\"2026-06-14\"")]
    [InlineData("\"tomorrow\"")]
    [InlineData("1781431200")]
    public async Task HttpJson_InstantWithoutAnOffset_IsRejected(string json)
    {
        // Arrange: it would otherwise be read in the server's time zone (ADR 0027).
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        Action act = () => JsonSerializer.Deserialize<DateTimeOffset?>(json, options);

        // Assert
        Should.Throw<JsonException>(act);
    }

    [Fact]
    public async Task HttpJson_InstantNull_StaysNull()
    {
        // Arrange
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        DateTimeOffset? instant = JsonSerializer.Deserialize<DateTimeOffset?>("null", options);

        // Assert
        instant.ShouldBeNull();
    }

    [Fact]
    public async Task HttpJson_NumberAsString_IsRejected()
    {
        // Arrange: the Web defaults would read "21" as 21, and the OpenAPI schema would say integer|string (ADR 0027).
        await using ApiFactory factory = new(postgres);
        JsonSerializerOptions options = factory.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Act
        Action act = () => JsonSerializer.Deserialize<int>("\"21\"", options);

        // Assert
        Should.Throw<JsonException>(act);
    }
}
