using AppTemplate.Application.Abstractions;

namespace AppTemplate.Application.Auth;

// Shared by the handlers that sign a user in: one access token for the user's current roles plus a fresh refresh
// token family.
internal static class TokenPairs
{
    public static async Task<TokenPairResponse> IssueAsync(
        IIdentityService identity,
        ITokenService tokens,
        UserAccount user,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> roles = await identity.GetRolesAsync(user.Id, cancellationToken);
        AccessToken access = tokens.IssueAccessToken(user, roles);
        RefreshToken refresh = await tokens.CreateRefreshTokenAsync(user.Id, cancellationToken);

        return new TokenPairResponse(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt);
    }
}
