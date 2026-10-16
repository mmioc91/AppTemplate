using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AppTemplate.Infrastructure.Mail;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AppTemplate.Integration.Tests.Mail;

// The SMTP transport against a real SMTP server (ADR 0034): Mailpit, the one docker-compose.yml runs, read back through
// its HTTP API.
public sealed class SmtpMailTransportTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    // The tag docker-compose.yml and ci-e2e.yml run: bump all three together.
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(ApiPort).ForPath("/api/v1/info")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

    [Fact]
    public async Task Send_MailOnSomeonesBehalf_ArrivesWithItsSenderReplyToAndHtml()
    {
        // Arrange
        var mail = new QueuedMail
        {
            Id = Guid.CreateVersion7(),
            To = "ana@example.com",
            Subject = "Welcome to Summer Cup",
            HtmlBody = "<p>Dobrodošli!</p>",
            SenderName = "Summer Cup",
            ReplyTo = "cup@example.com",
        };

        // Act
        await Transport().SendAsync(mail, CancellationToken.None);

        // Assert
        Message message = (await MessagesAsync()).ShouldHaveSingleItem();
        (message.Subject, message.From.Name, message.From.Address, message.To.ShouldHaveSingleItem().Address, message.ReplyTo.ShouldHaveSingleItem().Address)
            .ShouldBe(("Welcome to Summer Cup", "Summer Cup", "noreply@apptemplate.test", "ana@example.com", "cup@example.com"));
        (await HtmlOfAsync(message.Id)).ShouldContain("<p>Dobrodošli!</p>");
    }

    [Fact]
    public async Task Send_NoSenderOfItsOwn_ComesFromTheAppWithNoReplyTo()
    {
        // Act
        await Transport().SendAsync(
            new QueuedMail { Id = Guid.CreateVersion7(), To = "ana@example.com", Subject = "Notice", HtmlBody = "<p>Hi</p>" },
            CancellationToken.None);

        // Assert
        Message message = (await MessagesAsync()).ShouldHaveSingleItem();
        (message.From.Name, message.ReplyTo.Count).ShouldBe(("AppTemplate", 0));
    }

    private SmtpMailTransport Transport() =>
        new(Options.Create(new MailOptions
        {
            FromAddress = "noreply@apptemplate.test",
            FromName = "AppTemplate",
            SmtpHost = _mailpit.Hostname,
            SmtpPort = _mailpit.GetMappedPublicPort(SmtpPort),
            SmtpSecurity = MailSecurity.None,
        }));

    private async Task<List<Message>> MessagesAsync()
    {
        using HttpClient api = Api();
        return (await api.GetFromJsonAsync<Messages>("/api/v1/messages")).ShouldNotBeNull().List;
    }

    private async Task<string> HtmlOfAsync(string id)
    {
        using HttpClient api = Api();
        return (await api.GetFromJsonAsync<MessageBody>($"/api/v1/message/{id}")).ShouldNotBeNull().Html;
    }

    private HttpClient Api() => new() { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}") };

    // Mailpit's API, as much of it as the tests read.
    private sealed record Messages([property: JsonPropertyName("messages")] List<Message> List);

    private sealed record Message(string Id, string Subject, Mailbox From, List<Mailbox> To, List<Mailbox> ReplyTo);

    private sealed record Mailbox(string Name, string Address);

    private sealed record MessageBody(string Html);
}
