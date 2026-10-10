using System.Text.Json;
using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

public class BadRequestExceptionHandlerTests
{
    // The status mapping is checked here directly: a 413 needs a body over Kestrel's limit, and malformed JSON over HTTP
    // needs an endpoint with a body (test that with the first one, ADR 0018).
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ProblemCodes.MalformedRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ProblemCodes.RequestRejected)]
    public async Task TryHandleAsync_UnreadableRequest_AnswersWithItsOwnStatusAndACode(int statusCode, string expectedCode)
    {
        // Arrange
        DefaultHttpContext context = ProblemResponse.NewHttpContext();
        BadRequestExceptionHandler handler = NewHandler(context);

        // Act
        bool handled = await handler.TryHandleAsync(context, new BadHttpRequestException("Unreadable.", statusCode), CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(statusCode);
        JsonElement body = ProblemResponse.ReadBody(context);
        body.GetProperty("code").GetString().ShouldBe(expectedCode);
        body.TryGetProperty("detail", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_AnyOtherException_LeavesItToTheGeneric500()
    {
        // Arrange
        DefaultHttpContext context = ProblemResponse.NewHttpContext();
        BadRequestExceptionHandler handler = NewHandler(context);

        // Act
        bool handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
        context.Response.Body.Length.ShouldBe(0);
    }

    private static BadRequestExceptionHandler NewHandler(HttpContext context) =>
        new(context.RequestServices.GetRequiredService<IProblemDetailsService>(), NullLogger<BadRequestExceptionHandler>.Instance);
}
