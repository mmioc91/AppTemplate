using System.ComponentModel.DataAnnotations;
using AppTemplate.Infrastructure.Mail;
using Shouldly;

namespace AppTemplate.Integration.Tests.Mail;

// What the Api's ValidateDataAnnotations checks on start (ADR 0034).
public sealed class MailOptionsTests
{
    [Theory]
    [InlineData(MailSecurity.StartTls)]
    [InlineData(MailSecurity.SslOnConnect)]
    public void Validate_PasswordOverTls_IsValid(MailSecurity security)
    {
        // Act
        IReadOnlyList<ValidationResult> errors = Errors(Options(security, "a-password"));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NoTlsWithoutPassword_IsValid()
    {
        // Act: Mailpit in Development.
        IReadOnlyList<ValidationResult> errors = Errors(Options(MailSecurity.None, null));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_PasswordWithoutTls_IsInvalid()
    {
        // Act
        IReadOnlyList<ValidationResult> errors = Errors(Options(MailSecurity.None, "a-password"));

        // Assert
        errors.ShouldHaveSingleItem().MemberNames.ShouldContain(nameof(MailOptions.SmtpPassword));
    }

    private static MailOptions Options(MailSecurity security, string? password) =>
        new() { FromAddress = "noreply@apptemplate.test", SmtpHost = "smtp.apptemplate.test", SmtpSecurity = security, SmtpPassword = password };

    private static List<ValidationResult> Errors(MailOptions options)
    {
        List<ValidationResult> errors = [];
        Validator.TryValidateObject(options, new ValidationContext(options), errors, validateAllProperties: true);
        return errors;
    }
}
