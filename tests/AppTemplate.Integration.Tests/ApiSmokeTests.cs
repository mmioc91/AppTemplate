using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AppTemplate.Integration.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Root_endpoint_returns_ok()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
