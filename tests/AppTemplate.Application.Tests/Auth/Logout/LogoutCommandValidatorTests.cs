using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Logout;
using AppTemplate.Application.Common;
using FluentValidation.TestHelper;

namespace AppTemplate.Application.Tests.Auth.Logout;

public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        // Act
        TestValidationResult<LogoutCommand> result = _validator.TestValidate(new LogoutCommand("some-refresh-token"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_BlankToken_IsRequired(string? token)
    {
        // Act
        TestValidationResult<LogoutCommand> result = _validator.TestValidate(new LogoutCommand(token));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Fact]
    public void Validate_TokenOverMaxLength_IsTooLong()
    {
        // Act
        TestValidationResult<LogoutCommand> result = _validator.TestValidate(new LogoutCommand(new string('t', AuthRules.RefreshTokenMaxLength + 1)));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken).WithErrorCode(ValidationCodes.TooLong).Only();
    }
}
