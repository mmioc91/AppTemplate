using AppTemplate.Application.Common;
using FluentValidation;

namespace AppTemplate.Application.Auth.Login;

// Shape only: whether the pair matches an account is the handler's business. The length caps run here, before the
// handler, so an oversized input never reaches a lookup or a password hash (ADR 0031).
internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.EmailMaxLength).WithErrorCode(ValidationCodes.TooLong)
            .WithoutWhiteSpace().WithErrorCode(ValidationCodes.Invalid)
            .EmailAddress().WithErrorCode(ValidationCodes.Invalid);

        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.PasswordMaxLength).WithErrorCode(ValidationCodes.TooLong);
    }
}
