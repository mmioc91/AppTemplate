using System.ComponentModel.DataAnnotations;

namespace AppTemplate.Api.Auth;

// How many register and login calls one client may make a minute (ADR 0031): what slows down guessing. Ten is the
// production number; a host that runs the end-to-end suite — every test registering its own account from one address —
// raises it (appsettings.Development.json, which CI's end-to-end run uses too; ADR 0032). At least one: zero would
// switch signing in off, so the host refuses to start with it.
internal sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimits";

    public const int DefaultPerMinute = 10;

    [Range(1, int.MaxValue)]
    public int AuthPerMinute { get; set; } = DefaultPerMinute;
}
