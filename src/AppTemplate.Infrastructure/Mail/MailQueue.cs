using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Persistence;

namespace AppTemplate.Infrastructure.Mail;

// Adds the mail to the request's AppDbContext, so the use case's own SaveChanges commits it with the change (ADR 0034).
internal sealed class MailQueue(AppDbContext db, TimeProvider time) : IMailQueue
{
    public void Enqueue(OutgoingMail mail)
    {
        ArgumentNullException.ThrowIfNull(mail);
        DateTimeOffset now = time.GetUtcNow();

        db.QueuedMails.Add(new QueuedMail
        {
            Id = Guid.CreateVersion7(),
            To = mail.To,
            Subject = mail.Subject,
            HtmlBody = mail.HtmlBody,
            SenderName = mail.SenderName,
            ReplyTo = mail.ReplyTo,
            QueuedAt = now,
            NextAttemptAt = now,
        });
    }
}
