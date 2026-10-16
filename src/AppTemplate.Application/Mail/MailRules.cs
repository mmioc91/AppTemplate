using AppTemplate.Application.Auth;

namespace AppTemplate.Application.Mail;

// What the outbox's columns hold (QueuedMailConfiguration in Infrastructure); a validator checks the same where an
// address, a subject or a sender name comes from user input.
public static class MailRules
{
    // An address in mail is an address as sign-in knows it: one length, on purpose.
    public const int AddressMaxLength = AuthRules.EmailMaxLength;
    public const int SubjectMaxLength = 300;
    public const int SenderNameMaxLength = 100;
}
