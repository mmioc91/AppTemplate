using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Mail;

// Sends what is due from the queue (ADR 0034). A batch is claimed first — its rows' next attempt moved a lease ahead in
// one short statement with FOR UPDATE SKIP LOCKED, so two instances never take one mail — and then sent with no
// transaction open and no row locked, each mail's outcome saved as soon as it is known. A mail is delivered at least
// once: one whose outcome wasn't saved (the instance stopped, the database was gone) falls due again when its lease
// runs out.
internal sealed class MailDelivery(
    AppDbContext db,
    IMailTransport transport,
    TimeProvider time,
    IOptions<MailOptions> options,
    ILogger<MailDelivery> logger)
{
    // A failed mail waits longer each time; after the last wait it is given up.
    internal static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
    ];

    // Longer than a batch takes to send, so a mail still being sent doesn't fall due to another instance.
    internal static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);

    // Sent or given-up mail holds addresses; it is kept this long for support, then deleted.
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    // How many it took (sent or not); fewer than the batch size means the queue is drained for now.
    public async Task<int> DeliverDueAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset leasedUntil = now + Lease;
        int batchSize = options.Value.BatchSize;

        // One statement: retried on a dropped connection, it can't take a batch twice — at worst the first stays
        // claimed until its lease ends. Due first; the id only breaks ties, the send order below is the queue's.
        List<QueuedMail> claimed = await db.QueuedMails
            .FromSql($"""
                UPDATE queued_mails SET next_attempt_at = {leasedUntil}
                WHERE id IN (
                    SELECT id FROM queued_mails
                    WHERE sent_at IS NULL AND failed_at IS NULL AND next_attempt_at <= {now}
                    ORDER BY next_attempt_at, id
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(cancellationToken);

        foreach (QueuedMail mail in claimed.OrderBy(mail => mail.QueuedAt).ThenBy(mail => mail.Id))
        {
            await SendAsync(mail, cancellationToken);

            // Not cancelled with the round: what the server already took is recorded even while the host stops.
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return claimed.Count;
    }

    // Deletes what was sent or given up before the retention period; a bulk delete of Infrastructure's own rows.
    public Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset before = time.GetUtcNow() - Retention;

        return db.QueuedMails
            .Where(mail => mail.SentAt < before || mail.FailedAt < before)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Any failure of the transport counts as an attempt: an unreachable server, a refused address, a timeout. Only the
    // exception's type is logged — its message can hold the recipient's address.
    private async Task SendAsync(QueuedMail mail, CancellationToken cancellationToken)
    {
        try
        {
            await transport.SendAsync(mail, cancellationToken);
            mail.SentAt = time.GetUtcNow();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            mail.Attempts++;
            DateTimeOffset now = time.GetUtcNow();

            if (mail.Attempts > Backoff.Length)
            {
                mail.FailedAt = now;
                MailLog.GivenUp(logger, mail.Id, mail.Attempts, exception.GetType().Name);
            }
            else
            {
                mail.NextAttemptAt = now + Backoff[mail.Attempts - 1];
                MailLog.NotDelivered(logger, mail.Id, mail.Attempts, exception.GetType().Name);
            }
        }
    }
}
