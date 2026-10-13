using AppTemplate.Application.Common;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

// The shared codes are API contract (ADR 0023): clients switch on them, so a rename is a breaking change.
public class CommonErrorsTests
{
    [Fact]
    public void ConcurrencyConflict_IsAConflictWithTheStableCode()
    {
        // Act
        Error error = CommonErrors.ConcurrencyConflict();

        // Assert
        error.Kind.ShouldBe(ErrorKind.Conflict);
        error.Code.ShouldBe("CONCURRENCY_CONFLICT");
        error.FieldErrors.ShouldBeEmpty();
    }
}
