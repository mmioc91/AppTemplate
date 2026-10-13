using System.Text.Json;
using AppTemplate.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

public class ProblemDetailsSetupTests
{
    [Fact]
    public async Task Write_WithoutCode_GetsTheCodeForItsStatus()
    {
        // Act
        JsonElement body = await WriteAsync(StatusCodes.Status404NotFound, code: null);

        // Assert
        body.GetProperty("code").GetString().ShouldBe(ProblemCodes.NotFound);
    }

    [Fact]
    public async Task Write_WithOwnCode_KeepsIt()
    {
        // Act
        JsonElement body = await WriteAsync(StatusCodes.Status409Conflict, "ORDER_DUPLICATE");

        // Assert
        body.GetProperty("code").GetString().ShouldBe("ORDER_DUPLICATE");
    }

    [Fact]
    public async Task Write_WithLibraryCodeInAnotherFormat_ReplacesItWithTheCodeForItsStatus()
    {
        // Act: e.g. a library's "UnsupportedApiVersion".
        JsonElement body = await WriteAsync(StatusCodes.Status400BadRequest, "UnsupportedApiVersion");

        // Assert
        body.GetProperty("code").GetString().ShouldBe(ProblemCodes.MalformedRequest);
    }

    [Fact]
    public async Task Write_OutsideATrace_CarriesTheRequestsIdentifier()
    {
        // Arrange: no Activity here, so the request's own identifier stands in (ADR 0028).
        DefaultHttpContext context = ProblemResponse.NewHttpContext();

        // Act
        JsonElement body = await WriteAsync(StatusCodes.Status500InternalServerError, code: null, context);

        // Assert
        body.GetProperty(ProblemDetailsSetup.TraceIdKey).GetString().ShouldBe(context.TraceIdentifier);
    }

    private static Task<JsonElement> WriteAsync(int status, string? code) => WriteAsync(status, code, ProblemResponse.NewHttpContext());

    private static async Task<JsonElement> WriteAsync(int status, string? code, DefaultHttpContext context)
    {
        context.Response.StatusCode = status;
        ProblemDetails problem = new() { Status = status };
        if (code is not null)
        {
            problem.Extensions[ProblemCodes.Key] = code;
        }

        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return ProblemResponse.ReadBody(context);
    }
}
