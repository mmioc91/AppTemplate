using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppTemplate.Infrastructure.Mail;

// SMTP through MailKit: any provider speaks it (Brevo, SES, Postmark — ADR 0034). One connection per mail: enough for
// an app's notifications; a provider's HTTP API or a pooled connection can replace it without touching the use cases.
internal sealed class SmtpMailTransport(IOptions<MailOptions> options) : IMailTransport
{
    public async Task SendAsync(QueuedMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);
        MailOptions settings = options.Value;

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mail.SenderName ?? settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.To));

        if (mail.ReplyTo is { } replyTo)
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = mail.Subject;
        message.Body = new BodyBuilder { HtmlBody = mail.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, Security(settings.SmtpSecurity), cancellationToken);

        if (!string.IsNullOrEmpty(settings.SmtpUsername))
        {
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    private static SecureSocketOptions Security(MailSecurity security) =>
        security switch
        {
            MailSecurity.None => SecureSocketOptions.None,
            MailSecurity.StartTls => SecureSocketOptions.StartTls,
            MailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new ArgumentOutOfRangeException(nameof(security), security, "No such mail security."),
        };
}
