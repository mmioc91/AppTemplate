using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Common;

namespace AppTemplate.Application.Auth.Register;

internal sealed class RegisterHandler(IIdentityService identity, ITokenService tokens)
    : ICommandHandler<RegisterCommand, TokenPairResponse>
{
    public async Task<Result<TokenPairResponse>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        string displayName = command.DisplayName!.Trim();

        CreateUserOutcome outcome = await identity.CreateUserAsync(
            id,
            command.Email!,
            command.Password!,
            displayName,
            role: null,
            cancellationToken);

        switch (outcome)
        {
            case CreateUserOutcome.EmailTaken:
                return AuthErrors.EmailTaken();
            case CreateUserOutcome.EmailInvalid:
                // Identity's email rule is stricter than the validator's; the client sees the same field error.
                return FieldError(nameof(RegisterCommand.Email), ValidationCodes.Invalid);
            case CreateUserOutcome.PasswordTooShort:
                return FieldError(nameof(RegisterCommand.Password), ValidationCodes.TooShort);
            case CreateUserOutcome.Created:
                UserAccount user = new(id, command.Email!, displayName);
                return await TokenPairs.IssueAsync(identity, tokens, user, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome.");
        }
    }

    private static Error FieldError(string field, string code) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [code] });
}
