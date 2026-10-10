using AppTemplate.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Api.Errors;

// A broken domain invariant → 422 with the exception's code (ADR 0007). The message is English, may carry values and
// goes to the log only; the client shows its own text for the code (ADR 0018).
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        // An expected outcome, not a crash: no stack trace.
        logger.LogWarning("Domain rule {Code} was violated: {Message}", domainException.Code, domainException.Message);

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "The request breaks a business rule.",
        };
        problem.Extensions[ProblemCodes.Key] = domainException.Code;

        // Handled either way: a client that doesn't accept JSON still gets the 422, just without a body — never a 500.
        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
        return true;
    }
}
