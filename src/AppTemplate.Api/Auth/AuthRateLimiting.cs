using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AppTemplate.Api.Auth;

// Slows down credential guessing on register and login (ADR 0031): a fixed window per client, the host's number
// (AuthRateLimitOptions). Refresh has a window of its own: its token is 32 random bytes, nothing to guess, and every
// start of the web app spends one — the users behind one IP (an office, a venue) would share ten a minute. Registered
// with the other policies in ApiRateLimiting.
internal sealed class AuthRateLimiting(IOptions<AuthRateLimitOptions> options) : IRateLimiterPolicy<string>
{
    public const string PolicyName = "auth";

    public const string RefreshPolicyName = "auth-refresh";
    public const int RefreshPermitLimit = 60;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // The limiter's own 429 (ApiRateLimiting), as for every other policy.
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        ApiRateLimiting.FixedWindowPerClient(httpContext, options.Value.AuthPerMinute, Window);
}
