namespace AppTemplate.Application.Mail;

// A message with placeholders: Subject is plain text, Body an HTML fragment. A project's templates are static fields of
// a MailTemplates class in this folder, each listing its placeholders.
public sealed record MailTemplate(string Subject, string Body);
