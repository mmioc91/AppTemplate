using System.ComponentModel.DataAnnotations;

namespace AppTemplate.Api.Auth;

// How many register and login calls one client may make a minute (ADR 0031): what slows down guessing. Ten is the
// production number; a host that runs the end-to-end suite — every test registering its own account from one address —
// raises it (appsettings.Development.json, RateLimits__AuthPerMinute in CI; ADR 0032).
internal sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimits";

    public const int DefaultPerMinute = 10;

    [Range(1, int.MaxValue)]
    public int AuthPerMinute { get; set; } = DefaultPerMinute;
}
