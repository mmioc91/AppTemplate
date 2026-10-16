using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using NSubstitute;

namespace AppTemplate.Application.Tests.Auth;

// Fixed inputs shared by the auth handler tests, and a token port that answers like the real one would.
internal static class AuthSamples
{
    // Roles standing in for a project's own; the template ships none (Roles.All is empty).
    public const string Admin = "Admin";
    public const string Editor = "Editor";

    public static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    public static readonly UserAccount User = new(Guid.Parse("01a1231c-0000-7000-8000-00000000aaaa"), "ana@example.com", "Ana");

    public static readonly AccessToken Access = new("access-token", Now.AddMinutes(15));

    public static readonly RefreshToken Refresh = new("refresh-token", Now.AddDays(14));

    public static ITokenService TokenServiceIssuing(UserAccount user, IReadOnlyList<string> roles)
    {
        ITokenService tokens = Substitute.For<ITokenService>();
        tokens.IssueAccessToken(user, Arg.Is<IReadOnlyList<string>>(r => r.SequenceEqual(roles))).Returns(Access);
        tokens.CreateRefreshTokenAsync(user.Id, Arg.Any<CancellationToken>()).Returns(Refresh);
        return tokens;
    }
}
