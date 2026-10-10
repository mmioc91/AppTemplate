using AppTemplate.Application.Common;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

public class ResultOfTTests
{
    private static readonly Error SomeError = Error.Conflict("SOME_CONFLICT", "Something conflicts.");

    [Fact]
    public void Success_WithValue_IsSuccessAndExposesValue()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithError_IsFailureAndExposesError()
    {
        // Act
        var result = Result<int>.Failure(SomeError);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Failure(SomeError);

        // Act
        Func<int> act = () => result.Value;

        // Assert
        Should.Throw<InvalidOperationException>(() => act());
    }

    [Fact]
    public void Success_WithNullValue_ThrowsArgumentNullException()
    {
        // Act
        Func<Result<string>> act = () => Result<string>.Success(null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNullException()
    {
        // Act
        Func<Result<int>> act = () => Result<int>.Failure(null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccess()
    {
        // Act
        Result<string> result = "done";

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("done");
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        // Act
        Result<string> result = SomeError;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(SomeError);
    }
}
