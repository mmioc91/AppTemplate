using System.Text.Json;
using AppTemplate.Api.Errors;
using AppTemplate.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

public class DomainExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_DomainException_Writes422WithItsCodeButNotItsMessage()
    {
        // Arrange
        DefaultHttpContext context = ProblemResponse.NewHttpContext();
        DomainExceptionHandler handler = NewHandler(context);
        DomainException exception = new("ORDER_ARCHIVED", "Order ORD-4711 is archived.");

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        JsonElement body = ProblemResponse.ReadBody(context);
        body.GetProperty("code").GetString().ShouldBe("ORDER_ARCHIVED");
        body.TryGetProperty("detail", out _).ShouldBeFalse();
        body.GetRawText().ShouldNotContain("ORD-4711");
    }

    [Fact]
    public async Task TryHandleAsync_ClientNotAcceptingJson_StillAnswers422()
    {
        // Arrange: no ProblemDetails writer can serve text/plain, which must not turn a 422 into a 500.
        DefaultHttpContext context = ProblemResponse.NewHttpContext();
        context.Request.Headers.Accept = "text/plain";
        DomainExceptionHandler handler = NewHandler(context);

        // Act
        bool handled = await handler.TryHandleAsync(context, new DomainException("ORDER_ARCHIVED", "Archived."), CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TryHandleAsync_AnyOtherException_LeavesItToTheGeneric500()
    {
        // Arrange
        DefaultHttpContext context = ProblemResponse.NewHttpContext();
        DomainExceptionHandler handler = NewHandler(context);

        // Act
        bool handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
        context.Response.Body.Length.ShouldBe(0);
    }

    private static DomainExceptionHandler NewHandler(HttpContext context) =>
        new(context.RequestServices.GetRequiredService<IProblemDetailsService>(), NullLogger<DomainExceptionHandler>.Instance);
}
