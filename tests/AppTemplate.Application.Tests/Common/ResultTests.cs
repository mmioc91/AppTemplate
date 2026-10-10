using AppTemplate.Application.Common;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

public class ResultTests
{
    private static readonly Error SomeError = Error.Conflict("SOME_CONFLICT", "Something conflicts.");

    [Fact]
    public void Success_IsSuccessWithoutError()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithError_IsFailureAndExposesError()
    {
        // Act
        var result = Result.Failure(SomeError);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNullException()
    {
        // Act
        Func<Result> act = () => Result.Failure(null!);

        // Assert
        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        // Act
        Result result = SomeError;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(SomeError);
    }
}
