using AppTemplate.Application.Common;
using FluentValidation;

namespace AppTemplate.Application.Auth.Refresh;

internal sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(c => c.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ValidationCodes.Required)
            .MaximumLength(AuthRules.RefreshTokenMaxLength).WithErrorCode(ValidationCodes.TooLong);
    }
}
