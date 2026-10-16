namespace AppTemplate.Infrastructure.Mail;

// Hands one mail to the mail server; throws when it is refused or unreachable.
internal interface IMailTransport
{
    Task SendAsync(QueuedMail mail, CancellationToken cancellationToken);
}
