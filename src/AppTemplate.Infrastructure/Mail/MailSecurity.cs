namespace AppTemplate.Infrastructure.Mail;

// How the SMTP connection is secured (MailOptions.SmtpSecurity): None only for a server on the same machine, Mailpit.
public enum MailSecurity
{
    None,
    StartTls,
    SslOnConnect,
}
