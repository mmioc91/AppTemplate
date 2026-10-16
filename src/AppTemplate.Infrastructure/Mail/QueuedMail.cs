namespace AppTemplate.Infrastructure.Mail;

// One mail in the queued_mails table (ADR 0034): the outbox behind IMailQueue. Not an aggregate — Infrastructure's own
// bookkeeping, like the refresh tokens.
internal sealed class QueuedMail
{
    public Guid Id { get; init; }

    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string HtmlBody { get; init; } = string.Empty;

    public string? SenderName { get; init; }

    public string? ReplyTo { get; init; }

    public DateTimeOffset QueuedAt { get; init; }

    // When the next attempt may run: at once when queued, a lease ahead while claimed, later after each failure.
    public DateTimeOffset NextAttemptAt { get; set; }

    // Failed attempts so far.
    public int Attempts { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    // Given up after the last retry.
    public DateTimeOffset? FailedAt { get; set; }
}
