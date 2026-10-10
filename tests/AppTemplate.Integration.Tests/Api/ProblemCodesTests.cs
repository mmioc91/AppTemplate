using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

public class ProblemCodesTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ProblemCodes.MalformedRequest)]
    [InlineData(StatusCodes.Status401Unauthorized, ProblemCodes.Unauthenticated)]
    [InlineData(StatusCodes.Status403Forbidden, ProblemCodes.Forbidden)]
    [InlineData(StatusCodes.Status404NotFound, ProblemCodes.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ProblemCodes.MethodNotAllowed)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.UnsupportedMediaType)]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ProblemCodes.RequestRejected)]
    [InlineData(StatusCodes.Status500InternalServerError, ProblemCodes.UnexpectedError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ProblemCodes.UnexpectedError)]
    public void ForStatus_FrameworkError_HasAStableCode(int statusCode, string expectedCode)
    {
        // Act
        string code = ProblemCodes.ForStatus(statusCode);

        // Assert
        code.ShouldBe(expectedCode);
    }

    [Theory]
    [InlineData("ORDER_DUPLICATE")]
    [InlineData("REQUIRED")]
    [InlineData("V2_ONLY")]
    public void IsWellFormed_UpperSnakeCase_IsTrue(string code)
    {
        // Act & Assert
        ProblemCodes.IsWellFormed(code).ShouldBeTrue();
    }

    [Theory]
    [InlineData("UnsupportedApiVersion")]
    [InlineData("package_duplicate")]
    [InlineData("_LEADING")]
    [InlineData("2FA_REQUIRED")]
    [InlineData("WITH SPACE")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(42)]
    public void IsWellFormed_AnythingElse_IsFalse(object? code)
    {
        // Act & Assert
        ProblemCodes.IsWellFormed(code).ShouldBeFalse();
    }
}
