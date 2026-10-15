using System.Globalization;
using System.Threading.RateLimiting;
using AppTemplate.Api.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace AppTemplate.Api;

// Every rate-limit policy, registered once (ADR 0031): a fixed window per client IP; a rejection is a 429
// ProblemDetails with a code through the usual status-code pipeline, plus Retry-After. Behind the proxy the client's IP
// comes from X-Forwarded-For, trusted from Proxy:KnownNetworks only (ProxySetup, ADR 0029). A policy for signed-in
// users keys on the principal's sub claim instead (the limiter runs after authentication) and falls back to the IP.
internal static class ApiRateLimiting
{
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            AddFixedWindowPerClient(options, AuthRateLimiting.PolicyName, AuthRateLimiting.PermitLimit, AuthRateLimiting.Window);
            AddFixedWindowPerClient(options, AuthRateLimiting.RefreshPolicyName, AuthRateLimiting.RefreshPermitLimit, AuthRateLimiting.Window);
        });

    private static void AddFixedWindowPerClient(RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window) =>
        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));
}
