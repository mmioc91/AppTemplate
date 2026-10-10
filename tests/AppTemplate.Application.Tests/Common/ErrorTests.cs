using AppTemplate.Application.Common;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

public class ErrorTests
{
    [Fact]
    public void NotFound_WithCodeAndMessage_HasNotFoundKind()
    {
        // Act
        var error = Error.NotFound("ITEM_NOT_FOUND", "Item not found.");

        // Assert
        error.Code.ShouldBe("ITEM_NOT_FOUND");
        error.Kind.ShouldBe(ErrorKind.NotFound);
        error.Message.ShouldBe("Item not found.");
        error.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Conflict_WithCodeAndMessage_HasConflictKind()
    {
        // Act
        var error = Error.Conflict("ITEM_DUPLICATE", "Already tracked.");

        // Assert
        error.Code.ShouldBe("ITEM_DUPLICATE");
        error.Kind.ShouldBe(ErrorKind.Conflict);
        error.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Forbidden_WithCodeAndMessage_HasForbiddenKind()
    {
        // Act
        var error = Error.Forbidden("QUOTA_EXCEEDED", "Plan limit reached.");

        // Assert
        error.Code.ShouldBe("QUOTA_EXCEEDED");
        error.Kind.ShouldBe(ErrorKind.Forbidden);
        error.FieldErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Factories_WithBlankCode_ThrowArgumentException(string? code)
    {
        // Act
        Func<Error> notFound = () => Error.NotFound(code!, "message");
        Func<Error> conflict = () => Error.Conflict(code!, "message");
        Func<Error> forbidden = () => Error.Forbidden(code!, "message");

        // Assert
        Should.Throw<ArgumentException>(notFound);
        Should.Throw<ArgumentException>(conflict);
        Should.Throw<ArgumentException>(forbidden);
    }

    [Fact]
    public void Validation_WithFieldErrors_HasValidationKindAndCode()
    {
        // Arrange
        Dictionary<string, string[]> fieldErrors = new()
        {
            ["Name"] = ["NAME_REQUIRED"],
        };

        // Act
        var error = Error.Validation(fieldErrors);

        // Assert
        error.Code.ShouldBe(Error.ValidationFailedCode);
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors["Name"].ShouldBe(["NAME_REQUIRED"]);
    }

    [Fact]
    public void Equals_ValidationErrorsWithSameFieldErrorsInDifferentDictionaries_AreEqual()
    {
        // Arrange
        var first = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"] });
        var second = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"] });

        // Act
        bool equal = first == second;

        // Assert
        equal.ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_ValidationErrorsWithDifferentCodes_AreNotEqual()
    {
        // Arrange
        var first = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"] });
        var second = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["TOO_LONG"] });

        // Act
        bool equal = first == second;

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Equals_ValidationErrorsForDifferentFields_AreNotEqual()
    {
        // Arrange
        var first = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"] });
        var second = Error.Validation(new Dictionary<string, string[]> { ["Title"] = ["REQUIRED"] });

        // Act
        bool equal = first == second;

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Equals_ValidationErrorsDifferingInOneOfSeveralFields_AreNotEqual()
    {
        // Arrange: every field must match, not just one of them.
        var first = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"], ["Title"] = ["TOO_LONG"] });
        var second = Error.Validation(new Dictionary<string, string[]> { ["Name"] = ["REQUIRED"], ["Title"] = ["INVALID"] });

        // Act
        bool equal = first == second;

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Equals_ErrorsWithDifferentCodes_AreNotEqual()
    {
        // Act
        bool equal = Error.NotFound("ITEM_NOT_FOUND", "m") == Error.NotFound("OWNER_NOT_FOUND", "m");

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Equals_ErrorsWithDifferentKinds_AreNotEqual()
    {
        // Act
        bool equal = Error.NotFound("X", "m") == Error.Conflict("X", "m");

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Validation_WithNoFieldErrors_ThrowsArgumentException()
    {
        // Act
        Func<Error> act = () => Error.Validation(new Dictionary<string, string[]>());

        // Assert
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Validation_WithNullFieldErrors_ThrowsArgumentNullException()
    {
        // Act
        Func<Error> act = () => Error.Validation(null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }
}
