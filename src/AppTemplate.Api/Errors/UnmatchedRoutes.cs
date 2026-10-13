namespace AppTemplate.Api.Errors;

internal static class UnmatchedRoutes
{
    // A request that matched no endpoint is a 404 (ADR 0018). A fallback authorization policy also covers requests
    // without an endpoint, so without this the authorization middleware would answer 401 and hide the unknown route
    // behind a login. Runs after routing, before authorization; the status-code pages add the ProblemDetails body.
    public static IApplicationBuilder UseNotFoundForUnmatchedRoutes(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.GetEndpoint() is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return next(context);
        });
}
