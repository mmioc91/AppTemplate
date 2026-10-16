using System.Collections.Concurrent;
using System.Net;
using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Mail;
using HandlebarsDotNet;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Mail;

// Fills a message template with Handlebars (ADR 0034): {{name}} is HTML-escaped in the body and taken as it is in the
// subject, which is plain text; {{#if name}} shows a part only when the value isn't blank. The body goes into one
// layout with inline styles — what mail clients render — in the app's color.
internal sealed class HandlebarsMailRenderer(IOptions<MailOptions> options) : IMailRenderer
{
    // The web app's primary (Material's azure palette, web/src/styles.scss), so a mail looks like the app.
    private const string PrimaryColor = "#005cbb";

    private static readonly IHandlebars Body = Handlebars.Create();
    private static readonly IHandlebars Subject = Handlebars.Create(new HandlebarsConfiguration { NoEscape = true });

    // Compiled once per text: the templates are the app's own, a handful in all.
    private static readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> Bodies = new();
    private static readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> Subjects = new();

    public RenderedMail Render(MailTemplate template, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var data = values.ToDictionary(value => value.Key, value => string.IsNullOrWhiteSpace(value.Value) ? null : value.Value);

        string subject = Subjects.GetOrAdd(template.Subject, Subject.Compile)(data).Trim();
        string body = Bodies.GetOrAdd(template.Body, Body.Compile)(data);

        return new RenderedMail(subject, Layout(subject, body));
    }

    private string Layout(string subject, string body) =>
        $"""
        <!doctype html>
        <html lang="{WebUtility.HtmlEncode(options.Value.Language)}">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>{WebUtility.HtmlEncode(subject)}</title></head>
        <body style="margin:0;padding:0;background:#f4f5f7;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f5f7;padding:24px 0;">
        <tr><td align="center">
        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;border-top:6px solid {PrimaryColor};">
        <tr><td style="padding:24px 32px;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.5;color:#1f2933;">
        {body}
        </td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;
}
