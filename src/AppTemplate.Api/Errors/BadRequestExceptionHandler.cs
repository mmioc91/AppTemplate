using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Api.Errors;

// A request the server can't read — malformed JSON, a body over the size limit — carries its own status (400, 413…).
// In Development, Minimal API binding throws this instead of answering; without this handler it became a 500
// (ADR 0018). The code comes from the status (MALFORMED_REQUEST for 400).
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<BadRequestExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        // The client's mistake, not a crash: no stack trace.
        logger.LogWarning("Unreadable request answered with {StatusCode}: {Message}", badRequest.StatusCode, badRequest.Message);

        httpContext.Response.StatusCode = badRequest.StatusCode;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = badRequest.StatusCode },
            Exception = exception,
        });
        return true;
    }
}
