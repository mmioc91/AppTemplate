using System.ComponentModel.DataAnnotations;

namespace AppTemplate.Infrastructure.Mail;

// How mail leaves (ADR 0034), bound in the Api's Program.cs with ValidateOnStart. Development talks to Mailpit
// (docker-compose.yml); elsewhere the host and sender come from the environment, the password from secrets.
public sealed class MailOptions : IValidatableObject
{
    public const string SectionName = "Mail";

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    // The app's name as the sender (appsettings.json), unless a mail names its own (OutgoingMail.SenderName).
    [Required]
    public string FromName { get; set; } = "AppTemplate";

    // The language the templates are written in (appsettings.json), for the layout's lang attribute: the market's
    // once a project writes its templates (web/'s locale).
    [Required]
    public string Language { get; set; } = "en";

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public MailSecurity SmtpSecurity { get; set; } = MailSecurity.StartTls;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    // Off where nothing should leave (the tests, a stack tried locally); the queue still fills.
    public bool DeliveryEnabled { get; set; } = true;

    [Range(1, 3600)]
    public int PollSeconds { get; set; } = 10;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    // A password never crosses the network in the clear: an SMTP server without TLS takes none.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SmtpSecurity == MailSecurity.None && !string.IsNullOrEmpty(SmtpPassword))
        {
            yield return new ValidationResult(
                "An SMTP password needs SmtpSecurity StartTls or SslOnConnect.",
                [nameof(SmtpSecurity), nameof(SmtpPassword)]);
        }
    }
}
