using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Refresh;
using AppTemplate.Application.Common;
using FluentValidation.TestHelper;

namespace AppTemplate.Application.Tests.Auth.Refresh;

public class RefreshCommandValidatorTests
{
    private readonly RefreshCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        // Act
        TestValidationResult<RefreshCommand> result = _validator.TestValidate(new RefreshCommand("some-refresh-token"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_BlankToken_IsRequired(string? token)
    {
        // Act
        TestValidationResult<RefreshCommand> result = _validator.TestValidate(new RefreshCommand(token));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Fact]
    public void Validate_TokenOverMaxLength_IsTooLong()
    {
        // Act
        TestValidationResult<RefreshCommand> result = _validator.TestValidate(new RefreshCommand(new string('t', AuthRules.RefreshTokenMaxLength + 1)));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RefreshToken).WithErrorCode(ValidationCodes.TooLong).Only();
    }
}
