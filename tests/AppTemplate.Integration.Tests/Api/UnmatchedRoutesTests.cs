using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// No server needed: the middleware looks at one thing, what routing matched (ADR 0018, 0029, 0031).
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
    public async Task Invoke_RouteEndpointMatched_CallsTheRest()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/probe"), 0, EndpointMetadataCollection.Empty, "probe"));
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

    [Fact]
    public async Task Invoke_RoutingRejection_AnswersItWithoutCallingTheRest()
    {
        // Arrange: routing's own 405/415 endpoint, a plain Endpoint — run before authorization, so an anonymous caller
        // learns the method is wrong, not that it should sign in.
        DefaultHttpContext context = new();
        context.SetEndpoint(new Endpoint(
            ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return Task.CompletedTask;
            },
            EndpointMetadataCollection.Empty,
            "405 HTTP Method Not Supported"));
        bool restCalled = false;
        RequestDelegate pipeline = Pipeline(_ =>
        {
            restCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await pipeline(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        restCalled.ShouldBeFalse();
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
