namespace AppTemplate.Application.Mail;

// What the outbox's columns hold (QueuedMailConfiguration in Infrastructure); a validator checks the same where a
// subject or a sender name comes from user input.
public static class MailRules
{
    public const int SubjectMaxLength = 300;
    public const int SenderNameMaxLength = 100;
}
