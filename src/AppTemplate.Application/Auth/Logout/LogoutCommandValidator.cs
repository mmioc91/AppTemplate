using AppTemplate.Application.Common;
using FluentValidation;

namespace AppTemplate.Application.Auth.Logout;

internal sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(c => c.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.RefreshTokenMaxLength).WithErrorCode(ValidationCodes.TooLong);
    }
}
