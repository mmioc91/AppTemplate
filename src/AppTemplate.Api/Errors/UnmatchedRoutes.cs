namespace AppTemplate.Api.Errors;

internal static class UnmatchedRoutes
{
    // A request that matched no endpoint is a 404 (ADR 0018). The fallback authorization policy also covers requests
    // without a real endpoint, so without this the authorization middleware would answer 401 and hide what is wrong
    // behind a login: an unknown route, but also a known route with the wrong method (405) or content type (415) —
    // routing's own rejection endpoints, which are no RouteEndpoint. Those answer here, as they would without
    // authentication; a real endpoint goes on to authorization. Runs after routing, before authorization; the
    // status-code pages add the ProblemDetails body.
    public static IApplicationBuilder UseNotFoundForUnmatchedRoutes(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            Endpoint? endpoint = context.GetEndpoint();

            if (endpoint is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            if (IsRoutingRejection(endpoint))
            {
                return endpoint.RequestDelegate!(context);
            }

            return next(context);
        });

    private static bool IsRoutingRejection(Endpoint endpoint) =>
        endpoint is not RouteEndpoint && endpoint.RequestDelegate is not null;
}
