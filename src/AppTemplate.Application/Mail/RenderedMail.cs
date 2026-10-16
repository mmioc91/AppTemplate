namespace AppTemplate.Application.Mail;

// A template filled in: the subject as text, the body as a whole HTML document.
public sealed record RenderedMail(string Subject, string HtmlBody);
