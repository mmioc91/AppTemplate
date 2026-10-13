using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// No server needed: the middleware looks at one thing, whether routing matched an endpoint (ADR 0018, 0029).
public class UnmatchedRoutesTests
{
    [Fact]
    public async Task Invoke_NoEndpoint_Answers404WithoutCallingTheRest()
    {
        // Arrange
        DefaultHttpContext context = new();
        bool restCalled = false;
        RequestDelegate pipeline = Pipeline(_ =>
        {
            restCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await pipeline(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        restCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Invoke_EndpointMatched_CallsTheRest()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "probe"));
        bool restCalled = false;
        RequestDelegate pipeline = Pipeline(_ =>
        {
            restCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await pipeline(context);

        // Assert
        restCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    // The middleware alone, followed by `rest` in place of the real pipeline.
    private static RequestDelegate Pipeline(RequestDelegate rest)
    {
        ApplicationBuilder app = new(new ServiceCollection().BuildServiceProvider());
        app.UseNotFoundForUnmatchedRoutes();
        app.Run(rest);
        return app.Build();
    }
}
