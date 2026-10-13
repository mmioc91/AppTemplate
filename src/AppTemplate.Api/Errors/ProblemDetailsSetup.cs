using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace AppTemplate.Api.Errors;

internal static class ProblemDetailsSetup
{
    public const string TraceIdKey = "traceId";

    // Every error response is ProblemDetails with a code: DomainException → 422 here, Result failures via
    // ErrorResults, and anything the framework writes (bad JSON, unknown route, unhandled exception → 500 without
    // details) gets a code from its status (ADR 0018). A library's own code in another format is replaced too, so
    // clients only ever see UPPER_SNAKE_CASE codes. Each carries the request's W3C trace id as traceId — the TraceId
    // every log entry of the request has in its scope (ADR 0028), so a report of an error finds its logs.
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions[TraceIdKey] = Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier;

            if (context.ProblemDetails.Extensions.TryGetValue(ProblemCodes.Key, out object? code) && ProblemCodes.IsWellFormed(code))
            {
                return;
            }

            context.ProblemDetails.Extensions[ProblemCodes.Key] =
                ProblemCodes.ForStatus(context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode);
        });

        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<BadRequestExceptionHandler>();

        return services;
    }
}
