using AppTemplate.Application.Mail;
using AppTemplate.Infrastructure.Mail;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AppTemplate.Integration.Tests.Mail;

// Filling a message template (ADR 0034).
public sealed class HandlebarsMailRendererTests
{
    private readonly HandlebarsMailRenderer _renderer = new(Options.Create(new MailOptions { Language = "hr" }));

    [Fact]
    public void Render_Values_FillTheSubjectAsTextAndTheBodyEscaped()
    {
        // Act
        RenderedMail mail = _renderer.Render(
            new MailTemplate("Welcome, {{name}}", "<p>{{name}}</p>"),
            new Dictionary<string, string?> { ["name"] = "Ana & <Ivo>" });

        // Assert
        mail.Subject.ShouldBe("Welcome, Ana & <Ivo>");
        mail.HtmlBody.ShouldContain("<p>Ana &amp; &lt;Ivo&gt;</p>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Render_BlankValue_HidesItsIfBlock(string? category)
    {
        // Act
        RenderedMail mail = _renderer.Render(
            new MailTemplate("s", "<p>The event{{#if category}}, category {{category}}{{/if}}.</p>"),
            new Dictionary<string, string?> { ["category"] = category });

        // Assert
        mail.HtmlBody.ShouldContain("<p>The event.</p>");
    }

    [Fact]
    public void Render_GivenValue_ShowsItsIfBlock()
    {
        // Act
        RenderedMail mail = _renderer.Render(
            new MailTemplate("s", "<p>The event{{#if category}}, category {{category}}{{/if}}.</p>"),
            new Dictionary<string, string?> { ["category"] = "Seniors" });

        // Assert
        mail.HtmlBody.ShouldContain("<p>The event, category Seniors.</p>");
    }

    [Fact]
    public void Render_Body_IsAWholeHtmlDocumentInTheConfiguredLanguageTitledByTheSubject()
    {
        // Act
        RenderedMail mail = _renderer.Render(new MailTemplate("A & B", "<p>Hi</p>"), new Dictionary<string, string?>());

        // Assert
        mail.HtmlBody.ShouldStartWith("<!doctype html>");
        mail.HtmlBody.ShouldContain("<html lang=\"hr\">");
        mail.HtmlBody.ShouldContain("<title>A &amp; B</title>");
        mail.HtmlBody.ShouldContain("<p>Hi</p>");
    }

    [Fact]
    public void Render_SameTemplateTwice_GivesEachItsOwnValues()
    {
        // Arrange: the compiled template is cached by its text, never the values.
        var template = new MailTemplate("Hello {{name}}", "<p>{{name}}</p>");

        // Act
        RenderedMail first = _renderer.Render(template, new Dictionary<string, string?> { ["name"] = "Ana" });
        RenderedMail second = _renderer.Render(template, new Dictionary<string, string?> { ["name"] = "Ivo" });

        // Assert
        (first.Subject, second.Subject).ShouldBe(("Hello Ana", "Hello Ivo"));
    }
}
