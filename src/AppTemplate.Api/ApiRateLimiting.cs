using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using AppTemplate.Api.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace AppTemplate.Api;

// Every rate-limit policy, registered once (ADR 0031): a fixed window per client (ClientKey); a rejection is a 429
// ProblemDetails with a code through the usual status-code pipeline, plus Retry-After. Behind the proxy the client's IP
// comes from X-Forwarded-For, trusted from Proxy:KnownNetworks only (ProxySetup, ADR 0029). A policy for signed-in
// users keys on the principal's sub claim instead (the limiter runs after authentication) and falls back to the IP.
internal static class ApiRateLimiting
{
    private const string UnknownClient = "unknown";

    // The network part of an IPv6 address that names one subscriber: a /64.
    private const int Ipv6PrefixBytes = 8;

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
                ClientKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));

    // Who a client is, for its window. An IPv4 address as it is — also when a dual-stack socket reports it IPv6-mapped,
    // so ::ffff:203.0.113.7 and 203.0.113.7 share one window. An IPv6 address by its /64: one host is usually given a
    // whole /64 and can pick a fresh address for every request, so a window per address would never fill.
    internal static string ClientKey(IPAddress? address)
    {
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        byte[] bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixBytes, bytes.Length - Ipv6PrefixBytes);
        return $"{new IPAddress(bytes)}/{Ipv6PrefixBytes * 8}";
    }
}
