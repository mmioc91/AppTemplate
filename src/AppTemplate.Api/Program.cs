using System.Text.Json.Serialization;
using AppTemplate.Api;
using AppTemplate.Api.Auth;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Api.Json;
using AppTemplate.Api.OpenApi;
using AppTemplate.Application;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Identity;
using Asp.Versioning;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Bind every Options class here (Application and Infrastructure only register defaults), like JwtOptions: a host with
// a missing or unusable setting fails at startup, not on the first request.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.TryGetSigningKey(out _),
        $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} must be base64 of at least {JwtOptions.MinSigningKeyBytes} bytes (README → Getting started).")
    .ValidateOnStart();
builder.Services.AddOptions<AuthRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(AuthRateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Every log entry of a request carries its W3C trace id (an incoming traceparent continues the caller's), and so does
// every ProblemDetails (ADR 0028). Production writes the entries as JSON lines (appsettings.Production.json). This
// restates the host's default, so nobody turns the ids off by accident.
builder.Logging.Configure(options =>
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);

builder.Services.AddApiProblemDetails();
builder.Services.AddApiProxy(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimiting();

// Enums travel as names only, which the frontend switches on; a number is rejected, not taken as an undefined value.
// An instant without an offset is rejected too, not read in the server's time zone, and a number is a JSON number,
// never "21" — so the OpenAPI schemas say integer, not integer|string (ADR 0027).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.SerializerOptions.Converters.Add(new StrictInstantConverter());
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = ApiVersions.V1;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    })
    // One OpenAPI document per version (/openapi/v1.json); a typed frontend client is generated from it (ADR 0009).
    // String query parameters with fixed values list them ([QueryValues]); instants and enums get the types the
    // generated client needs (ADR 0027).
    .AddOpenApi(options => options.Document
        .AddOperationTransformer<QueryValuesTransformer>()
        .AddSchemaTransformer<ClientTypesTransformer>());

WebApplication app = builder.Build();

// A deploy step, not a host (ADR 0029): the same image with Database__MigrateAndExit=true brings the schema up to date
// and stops, before the new version serves anyone. The host is disposed so the last log lines are flushed; there is
// no stopping token yet, as the host never starts.
if (app.Configuration.GetValue<bool>(AppTemplate.Infrastructure.DependencyInjection.MigrateAndExitKey))
{
    await using (app)
    {
        await app.Services.MigrateDatabaseAsync(CancellationToken.None);
    }

    return;
}

// The client's real address and scheme, from the proxies the configuration trusts — first, so everything after sees them.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
    app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

// Before authorization: the fallback policy requires a signed-in user, so an unknown route must stay a 404, not a 401.
app.UseNotFoundForUnmatchedRoutes();

// Called explicitly, here — registered without the calls, WebApplication inserts them ahead of every middleware above,
// and the 401 wins over the 404 again. The limiter runs after authentication, so a policy can count per account;
// checking a bearer token's signature is cheap, and the anonymous endpoints are still limited per IP before anything
// else runs (ADR 0031).
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthEndpoints();

// Each area maps its endpoints on this group: v1.MapOrders(); …
RouteGroupBuilder v1 = app.MapApiV1();
v1.MapAuth();

await app.RunAsync();
