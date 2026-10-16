namespace AppTemplate.Application.Abstractions;

// One mail to one recipient (ADR 0034): the rendered HTML, and a sender's display name and a reply-to address when the
// mail goes out on someone's behalf; otherwise the app's own. The addresses, the subject and the sender name fit the
// outbox's columns (MailRules): a validator limits them where they come from user input.
public sealed record OutgoingMail(string To, string Subject, string HtmlBody, string? SenderName = null, string? ReplyTo = null);
