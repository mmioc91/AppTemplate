using AppTemplate.Infrastructure.Web;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AppTemplate.Integration.Tests.Web;

// The links mail carries (ADR 0034): the web app's pages under Web:BaseUrl.
public sealed class WebLinksTests
{
    [Theory]
    [InlineData("https://apptemplate.example")]
    [InlineData("https://apptemplate.example/")]
    public void SignIn_BaseUrl_LinksTheSignInPage(string baseUrl)
    {
        // Act
        Uri link = Links(baseUrl).SignIn();

        // Assert
        link.AbsoluteUri.ShouldBe("https://apptemplate.example/login");
    }

    [Fact]
    public void Page_Token_GoesInTheQueryEscaped()
    {
        // Act
        Uri link = Links("https://apptemplate.example").Page("confirm", "a+b/c");

        // Assert
        link.AbsoluteUri.ShouldBe("https://apptemplate.example/confirm?token=a%2Bb%2Fc");
    }

    private static WebLinks Links(string baseUrl) => new(Options.Create(new WebOptions { BaseUrl = baseUrl }));
}
