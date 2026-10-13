using System.Text.Json.Serialization;
using AppTemplate.Api;
using AppTemplate.Api.Errors;
using AppTemplate.Api.Health;
using AppTemplate.Api.Json;
using AppTemplate.Api.OpenApi;
using AppTemplate.Application;
using AppTemplate.Infrastructure;
using Asp.Versioning;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Bind every Options class here (Application only registers defaults), e.g.:
// builder.Services.AddOptions<FooOptions>().Bind(builder.Configuration.GetSection(FooOptions.SectionName)).ValidateOnStart();

builder.Services.AddApiProblemDetails();

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

// Authentication goes here once the scheme is decided (docs/decisions/README.md → Open questions).

app.MapHealthEndpoints();

// Each area maps its endpoints on this group: RouteGroupBuilder v1 = app.MapApiV1(); v1.MapOrders(); …
app.MapApiV1();

await app.RunAsync();
