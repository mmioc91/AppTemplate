using System.Text.Json;
using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Integration.Tests.Api;

// Writes an error through the Api's real ProblemDetails setup, without a server, and reads back the JSON a client
// would receive.
internal static class ProblemResponse
{
    public static DefaultHttpContext NewHttpContext()
    {
        ServiceProvider services = new ServiceCollection().AddLogging().AddApiProblemDetails().BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services, Response = { Body = new MemoryStream() } };
    }

    public static JsonElement ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        return document.RootElement.Clone();
    }
}
