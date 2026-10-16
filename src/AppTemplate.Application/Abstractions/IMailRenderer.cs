using AppTemplate.Application.Mail;

namespace AppTemplate.Application.Abstractions;

// Turns a message template and its values into a mail (ADR 0034): placeholders like {{displayName}} filled in —
// HTML-escaped in the body, taken as they are in the subject, which is plain text — and the body wrapped in the app's
// mail layout.
public interface IMailRenderer
{
    RenderedMail Render(MailTemplate template, IReadOnlyDictionary<string, string?> values);
}
