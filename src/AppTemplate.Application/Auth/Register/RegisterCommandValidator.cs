using AppTemplate.Application.Common;
using FluentValidation;

namespace AppTemplate.Application.Auth.Register;

// Shape only. The minimum password length is checked here too, so a form gets every field error in one round trip;
// Identity enforces the same number (AuthRules) as the backstop, and the handler maps its refusal to the same field.
internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.EmailMaxLength).WithErrorCode(ValidationCodes.TooLong)
            .EmailAddress().WithErrorCode(ValidationCodes.Invalid);

        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MinimumLength(AuthRules.PasswordMinLength).WithErrorCode(ValidationCodes.TooShort)
            .MaximumLength(AuthRules.PasswordMaxLength).WithErrorCode(ValidationCodes.TooLong);

        RuleFor(c => c.DisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.DisplayNameMaxLength).WithErrorCode(ValidationCodes.TooLong);
    }
}
