using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth.Logout;

// Idempotent: an unknown or already revoked token is a logout that already happened, so it succeeds too. Only a token
// of the signed-in user is revoked — the user comes from the principal, never from the body.
internal sealed class LogoutHandler(ITokenService tokens, ICurrentUser currentUser) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        await tokens.RevokeRefreshTokenAsync(command.RefreshToken!, currentUser.UserId, cancellationToken);

        return Result.Success();
    }
}
