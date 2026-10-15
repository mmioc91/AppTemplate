using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Register;
using AppTemplate.Application.Common;
using FluentValidation.TestHelper;

namespace AppTemplate.Application.Tests.Auth.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand("ana@example.com", "correct horse battery", "Ana"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankEmail_IsRequired(string? email)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert: the stop cascade reports REQUIRED alone, not INVALID as well.
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Theory]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("@example.com")]
    public void Validate_MalformedEmail_IsInvalid(string email)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Theory]
    [InlineData("ana @example.com")]
    [InlineData(" ana@example.com")]
    [InlineData("ana@example.com\t")]
    [InlineData("ana\u0000@example.com")]
    public void Validate_EmailWithWhiteSpaceOrAControlCharacter_IsInvalid(string email)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert: refused before it is stored or looked up (Postgres refuses a NUL with a 500).
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Theory]
    [InlineData("o'brien@example.com")]
    [InlineData("ana+tag@example.com")]
    public void Validate_EmailWithCharactersAnAddressMayHave_IsAccepted(string email)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Validate_EmailOverMaxLength_IsTooLong()
    {
        // Arrange: a well-formed address one character over the limit.
        string email = new string('a', AuthRules.EmailMaxLength - "@example.com".Length + 1) + "@example.com";

        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email).WithErrorCode(ValidationCodes.TooLong).Only();
    }

    [Fact]
    public void Validate_EmailAtMaxLength_IsAccepted()
    {
        // Arrange
        string email = new string('a', AuthRules.EmailMaxLength - "@example.com".Length) + "@example.com";

        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand(email, "correct horse battery", "Ana"));

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_BlankPassword_IsRequired(string? password)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand("ana@example.com", password, "Ana"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Fact]
    public void Validate_PasswordOverMaxLength_IsTooLong()
    {
        // Act
        TestValidationResult<RegisterCommand> result =
            _validator.TestValidate(new RegisterCommand("ana@example.com", new string('p', AuthRules.PasswordMaxLength + 1), "Ana"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password).WithErrorCode(ValidationCodes.TooLong).Only();
    }

    [Fact]
    public void Validate_PasswordUnderMinLength_IsTooShort()
    {
        // Act
        TestValidationResult<RegisterCommand> result =
            _validator.TestValidate(new RegisterCommand("ana@example.com", new string('p', AuthRules.PasswordMinLength - 1), "Ana"));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Password).WithErrorCode(ValidationCodes.TooShort).Only();
    }

    [Fact]
    public void Validate_PasswordAtMinLength_IsAccepted()
    {
        // Act
        TestValidationResult<RegisterCommand> result =
            _validator.TestValidate(new RegisterCommand("ana@example.com", new string('p', AuthRules.PasswordMinLength), "Ana"));

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Password);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankDisplayName_IsRequired(string? displayName)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand("ana@example.com", "correct horse battery", displayName));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DisplayName).WithErrorCode(ValidationCodes.Required).Only();
    }

    [Theory]
    [InlineData("Ana\u0000")]
    [InlineData("Ana\nAnić")]
    public void Validate_DisplayNameWithAControlCharacter_IsInvalid(string displayName)
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand("ana@example.com", "correct horse battery", displayName));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DisplayName).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Fact]
    public void Validate_DisplayNameWithSpacesBetweenWords_IsAccepted()
    {
        // Act
        TestValidationResult<RegisterCommand> result = _validator.TestValidate(new RegisterCommand("ana@example.com", "correct horse battery", "Ana Marija Anić"));

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.DisplayName);
    }

    [Fact]
    public void Validate_DisplayNameOverMaxLength_IsTooLong()
    {
        // Act
        TestValidationResult<RegisterCommand> result =
            _validator.TestValidate(new RegisterCommand("ana@example.com", "correct horse battery", new string('n', AuthRules.DisplayNameMaxLength + 1)));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DisplayName).WithErrorCode(ValidationCodes.TooLong).Only();
    }
}
