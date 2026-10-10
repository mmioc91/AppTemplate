using System.Text.Json;
using AppTemplate.Api.Errors;
using AppTemplate.Application.Common;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

public class ErrorResultsTests
{
    public static TheoryData<Error, int> ExpectedFailures() => new()
    {
        { Error.NotFound("ORDER_NOT_FOUND", "Order 42 was not found."), StatusCodes.Status404NotFound },
        { Error.Conflict("ORDER_DUPLICATE", "Customer 7 already placed ORD-1."), StatusCodes.Status409Conflict },
        { Error.Forbidden("QUOTA_EXCEEDED", "Customer 7 reached the plan limit."), StatusCodes.Status403Forbidden },
    };

    [Theory]
    [MemberData(nameof(ExpectedFailures))]
    public async Task ToProblem_ExpectedFailure_WritesItsStatusAndCodeButNotTheMessage(Error error, int expectedStatus)
    {
        // Arrange
        DefaultHttpContext context = ProblemResponse.NewHttpContext();

        // Act
        await error.ToProblem().ExecuteAsync(context);

        // Assert
        JsonElement body = ProblemResponse.ReadBody(context);
        context.Response.StatusCode.ShouldBe(expectedStatus);
        context.Response.ContentType.ShouldBe("application/problem+json");
        body.GetProperty("status").GetInt32().ShouldBe(expectedStatus);
        body.GetProperty("code").GetString().ShouldBe(error.Code);
        body.TryGetProperty("detail", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ToProblem_ValidationFailure_Writes400WithFieldErrorsUnderJsonNames()
    {
        // Arrange: Application reports C# property paths, nested ones included.
        var error = Error.Validation(new Dictionary<string, string[]>
        {
            ["Name"] = ["REQUIRED"],
            ["Address.PostalCode"] = ["REQUIRED", "TOO_LONG"],
        });
        DefaultHttpContext context = ProblemResponse.NewHttpContext();

        // Act
        await error.ToProblem().ExecuteAsync(context);

        // Assert
        JsonElement body = ProblemResponse.ReadBody(context);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        body.GetProperty("code").GetString().ShouldBe(Error.ValidationFailedCode);
        JsonElement errors = body.GetProperty("errors");
        errors.EnumerateObject().Select(field => field.Name).ShouldBe(["name", "address.postalCode"], ignoreOrder: true);
        errors.GetProperty("address.postalCode").EnumerateArray().Select(code => code.GetString()).ShouldBe(["REQUIRED", "TOO_LONG"]);
    }
}
