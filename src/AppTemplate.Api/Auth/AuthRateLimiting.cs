namespace AppTemplate.Api.Auth;

// Slows down credential guessing on register and login (ADR 0031). Refresh has a window of its own: its token is 32
// random bytes, nothing to guess, and every start of the web app spends one — the users behind one IP (an office, a
// venue) would share ten a minute. Registered with the other policies in ApiRateLimiting.
internal static class AuthRateLimiting
{
    public const string PolicyName = "auth";
    public const int PermitLimit = 10;

    public const string RefreshPolicyName = "auth-refresh";
    public const int RefreshPermitLimit = 60;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
