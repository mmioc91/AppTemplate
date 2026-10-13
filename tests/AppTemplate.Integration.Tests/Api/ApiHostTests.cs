using System.Net;
using System.Text.Json;
using AppTemplate.Api;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Integration.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task ForwardedHeaders_KnownNetworksConfigured_TrustsOnlyThoseProxies()
    {
        // Arrange: the reverse proxy's network (ADR 0029).
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
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
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
