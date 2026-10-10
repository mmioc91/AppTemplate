using AppTemplate.SharedKernel;
using Shouldly;

namespace AppTemplate.Domain.Tests;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithCodeAndMessage_ExposesBoth()
    {
        // Act
        DomainException exception = new("SOME_CODE", "Something went wrong.");

        // Assert
        exception.Code.ShouldBe("SOME_CODE");
        exception.Message.ShouldBe("Something went wrong.");
    }

    [Fact]
    public void Constructor_WithInnerException_KeepsInnerException()
    {
        // Arrange
        InvalidOperationException inner = new("inner");

        // Act
        DomainException exception = new("SOME_CODE", "Something went wrong.", inner);

        // Assert
        exception.Code.ShouldBe("SOME_CODE");
        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankCode_ThrowsArgumentException(string? code)
    {
        // Act
        Func<DomainException> act = () => new DomainException(code!, "Something went wrong.");

        // Assert
        Should.Throw<ArgumentException>(act);
    }
}
