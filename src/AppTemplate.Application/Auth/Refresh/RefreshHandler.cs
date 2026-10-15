using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth.Refresh;

internal sealed class RefreshHandler(IIdentityService identity, ITokenService tokens)
    : ICommandHandler<RefreshCommand, TokenPairResponse>
{
    public async Task<Result<TokenPairResponse>> HandleAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        RefreshTokenRotation? rotation = await tokens.RotateRefreshTokenAsync(command.RefreshToken!, cancellationToken);

        if (rotation is null)
        {
            return AuthErrors.InvalidRefreshToken();
        }

        UserAccount? user = await identity.FindByIdAsync(rotation.UserId, cancellationToken);

        // The token outlived its user: nothing to sign in.
        if (user is null)
        {
            return AuthErrors.InvalidRefreshToken();
        }

        IReadOnlyList<string> roles = await identity.GetRolesAsync(user.Id, cancellationToken);
        AccessToken access = tokens.IssueAccessToken(user, roles);

        return new TokenPairResponse(access.Token, access.ExpiresAt, rotation.Replacement.Token, rotation.Replacement.ExpiresAt);
    }
}
