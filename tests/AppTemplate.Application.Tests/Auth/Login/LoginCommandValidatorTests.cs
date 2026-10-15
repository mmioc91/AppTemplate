using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Login;
using AppTemplate.Application.Common;
using FluentValidation.TestHelper;

namespace AppTemplate.Application.Tests.Auth.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand("ana@example.com", "correct horse battery"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_BlankEmail_IsRequired(string? email)
    {
        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand(email, "correct horse battery"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Fact]
    public void Validate_MalformedEmail_IsInvalid()
    {
        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand("ana", "correct horse battery"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Theory]
    [InlineData("ana @example.com")]
    [InlineData("ana\u0000@example.com")]
    public void Validate_EmailWithWhiteSpaceOrAControlCharacter_IsInvalid(string email)
    {
        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand(email, "correct horse battery"));

        // Assert: refused before the lookup (Postgres refuses a NUL with a 500).
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Fact]
    public void Validate_EmailOverMaxLength_IsTooLong()
    {
        // Arrange
        string email = new string('a', AuthRules.EmailMaxLength - "@example.com".Length + 1) + "@example.com";

        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand(email, "correct horse battery"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.TooLong).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_BlankPassword_IsRequired(string? password)
    {
        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(new LoginCommand("ana@example.com", password));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Fact]
    public void Validate_PasswordOverMaxLength_IsTooLong()
    {
        // Arrange: refused before anything is looked up or hashed (ADR 0031).
        LoginCommand command = new("ana@example.com", new string('p', AuthRules.PasswordMaxLength + 1));

        // Act
        TestValidationResult<LoginCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password).WithErrorCode(ValidationCodes.TooLong).Only();
    }
}
