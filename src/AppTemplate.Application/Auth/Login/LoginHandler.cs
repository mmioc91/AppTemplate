using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth.Login;

internal sealed class LoginHandler(IIdentityService identity, ITokenService tokens)
    : ICommandHandler<LoginCommand, TokenPairResponse>
{
    public async Task<Result<TokenPairResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        UserAccount? user = await identity.CheckPasswordAsync(command.Email!, command.Password!, cancellationToken);

        // An unknown email, a wrong password and a lockout answer alike, so the response never says whether the
        // account exists or is locked (ADR 0031).
        if (user is null)
        {
            return AuthErrors.InvalidCredentials();
        }

        return await TokenPairs.IssueAsync(identity, tokens, user, cancellationToken);
    }
}
