using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Mail;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence.Mail;

// The mail outbox (ADR 0034) on a database of its own, so no other test's mail is due here: queued with the unit of
// work, sent by MailDelivery through a transport that records what it was given.
[Collection(PostgresCollection.Name)]
public sealed class MailDeliveryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly RecordingTransport _transport = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        string database = await postgres.CreateEmptyDatabaseAsync();
        _services = postgres.CreateServices(database, services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            services.AddSingleton<IMailTransport>(_transport);
            services.AddLogging();
            services.AddOptions<MailOptions>().Configure(options =>
            {
                options.FromAddress = "noreply@apptemplate.test";
                options.SmtpHost = "localhost";
                options.BatchSize = 2;
                options.PollSeconds = 60;
            });
        });

        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task Enqueue_Saved_IsQueuedAndDueAtOnce()
    {
        // Act
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>", "Summer Cup", "cup@example.com"));

        // Assert
        QueuedMail mail = (await QueuedAsync()).ShouldHaveSingleItem();
        (mail.To, mail.Subject, mail.HtmlBody, mail.SenderName, mail.ReplyTo).ShouldBe(("ana@example.com", "Hello", "<p>Hi</p>", (string?)"Summer Cup", (string?)"cup@example.com"));
        (mail.QueuedAt, mail.NextAttemptAt, mail.Attempts, mail.SentAt, mail.FailedAt).ShouldBe((Now, Now, 0, (DateTimeOffset?)null, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task Enqueue_NeverSaved_QueuesNothing()
    {
        // Arrange: the change that caused it failed, so no unit of work was saved.
        await using (AsyncServiceScope scope = _services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IMailQueue>().Enqueue(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        }

        // Act
        List<QueuedMail> queued = await QueuedAsync();

        // Assert
        queued.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeliverDue_DueMail_IsSentAndMarkedSent()
    {
        // Arrange
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));

        // Act
        int taken = await DeliverAsync();

        // Assert
        taken.ShouldBe(1);
        _transport.Sent.ShouldHaveSingleItem().To.ShouldBe("ana@example.com");
        (await QueuedAsync()).ShouldHaveSingleItem().SentAt.ShouldBe(Now);
    }

    [Fact]
    public async Task DeliverDue_MoreThanABatch_SendsABatchAtATimeInQueueOrder()
    {
        // Arrange
        await QueueAsync(new OutgoingMail("a@example.com", "1", "1"));
        _clock.Advance(TimeSpan.FromSeconds(1));
        await QueueAsync(new OutgoingMail("b@example.com", "2", "2"));
        _clock.Advance(TimeSpan.FromSeconds(1));
        await QueueAsync(new OutgoingMail("c@example.com", "3", "3"));

        // Act
        int first = await DeliverAsync();
        int second = await DeliverAsync();

        // Assert
        (first, second).ShouldBe((2, 1));
        _transport.Sent.Select(mail => mail.To).ShouldBe(["a@example.com", "b@example.com", "c@example.com"]);
    }

    [Fact]
    public async Task DeliverDue_NotYetDue_IsLeftForLater()
    {
        // Arrange
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "ana@example.com", Subject = "s", HtmlBody = "b", QueuedAt = Now, NextAttemptAt = Now.AddSeconds(1) });

        // Act
        int taken = await DeliverAsync();

        // Assert
        taken.ShouldBe(0);
        _transport.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeliverDue_SentOrGivenUp_IsNotSentAgain()
    {
        // Arrange
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "a@example.com", Subject = "s", HtmlBody = "b", QueuedAt = Now, NextAttemptAt = Now, SentAt = Now });
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "b@example.com", Subject = "s", HtmlBody = "b", QueuedAt = Now, NextAttemptAt = Now, FailedAt = Now });

        // Act
        int taken = await DeliverAsync();

        // Assert
        taken.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 60)]
    [InlineData(4, 240)]
    public async Task DeliverDue_TransportFails_TriesAgainAfterALongerWaitEachTime(int failedBefore, int waitMinutes)
    {
        // Arrange
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "ana@example.com", Subject = "s", HtmlBody = "b", QueuedAt = Now, NextAttemptAt = Now, Attempts = failedBefore });
        _transport.Fails = true;

        // Act
        await DeliverAsync();

        // Assert
        QueuedMail mail = (await QueuedAsync()).ShouldHaveSingleItem();
        (mail.Attempts, mail.NextAttemptAt, mail.SentAt, mail.FailedAt).ShouldBe((failedBefore + 1, Now.AddMinutes(waitMinutes), (DateTimeOffset?)null, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task DeliverDue_FailureAfterTheLastWait_GivesUp()
    {
        // Arrange
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "ana@example.com", Subject = "s", HtmlBody = "b", QueuedAt = Now, NextAttemptAt = Now, Attempts = MailDelivery.Backoff.Length });
        _transport.Fails = true;

        // Act
        await DeliverAsync();

        // Assert
        QueuedMail mail = (await QueuedAsync()).ShouldHaveSingleItem();
        (mail.Attempts, mail.FailedAt).ShouldBe((MailDelivery.Backoff.Length + 1, (DateTimeOffset?)Now));
    }

    [Fact]
    public async Task DeliverDue_RetryOnceDue_IsSent()
    {
        // Arrange
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        _transport.Fails = true;
        await DeliverAsync();
        _transport.Fails = false;
        _clock.Advance(MailDelivery.Backoff[0]);

        // Act
        int taken = await DeliverAsync();

        // Assert
        taken.ShouldBe(1);
        (await QueuedAsync()).ShouldHaveSingleItem().SentAt.ShouldBe(Now + MailDelivery.Backoff[0]);
    }

    [Fact]
    public async Task DeliverDue_StoppedWhileSending_LeavesTheMailClaimedUntilItsLeaseEnds()
    {
        // Arrange: the host stops while the server has the mail — whether it took it, nobody knows.
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        using var stopping = new CancellationTokenSource();
        _transport.During = async _ =>
        {
            await stopping.CancelAsync();
            throw new OperationCanceledException(stopping.Token);
        };
        await Should.ThrowAsync<OperationCanceledException>(() => DeliverAsync(stopping.Token));
        _transport.During = null;

        // Act
        int duringTheLease = await DeliverAsync();
        _clock.Advance(MailDelivery.Lease);
        int afterIt = await DeliverAsync();

        // Assert: another instance doesn't take it meanwhile; afterwards it is sent — at least once (ADR 0034).
        (duringTheLease, afterIt).ShouldBe((0, 1));
        _transport.Sent.ShouldHaveSingleItem().To.ShouldBe("ana@example.com");
    }

    [Fact]
    public async Task DeliverDue_TransportCancelsOnItsOwn_CountsAFailedAttempt()
    {
        // Arrange: a transport's own timeout (an HTTP client's is a TaskCanceledException) is a failure like any
        // other, not the host stopping — one that escaped would stop the whole host.
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        _transport.During = _ => throw new TaskCanceledException();

        // Act
        int taken = await DeliverAsync();

        // Assert
        taken.ShouldBe(1);
        QueuedMail mail = (await QueuedAsync()).ShouldHaveSingleItem();
        (mail.Attempts, mail.NextAttemptAt, mail.SentAt).ShouldBe((1, Now + MailDelivery.Backoff[0], (DateTimeOffset?)null));
    }

    [Fact]
    public async Task DeliverDue_WhileSending_HoldsNoLockOnTheMail()
    {
        // Arrange: during the send, another connection asks for the row without waiting.
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        int lockedByAnother = 0;
        _transport.During = async mail =>
        {
            await using AsyncServiceScope other = _services.CreateAsyncScope();
            lockedByAnother = (await other.ServiceProvider.GetRequiredService<AppDbContext>().QueuedMails
                .FromSql($"SELECT * FROM queued_mails WHERE id = {mail.Id} FOR UPDATE NOWAIT")
                .ToListAsync()).Count;
        };

        // Act
        await DeliverAsync();

        // Assert: a lock held across the send would have refused it (lock_not_available).
        lockedByAnother.ShouldBe(1);
    }

    [Fact]
    public async Task DeliverDue_MailLockedByAnotherInstance_IsSkippedWithoutWaiting()
    {
        // Arrange: another instance holds the first mail's row in an open transaction.
        await QueueAsync(new OutgoingMail("a@example.com", "1", "1"));
        _clock.Advance(TimeSpan.FromSeconds(1));
        await QueueAsync(new OutgoingMail("b@example.com", "2", "2"));
        await using AsyncServiceScope other = _services.CreateAsyncScope();
        AppDbContext db = other.ServiceProvider.GetRequiredService<AppDbContext>();
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: delivery runs while the lock is held; a run that waited for it would be cancelled instead.
        int taken = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlAsync($"SELECT id FROM queued_mails WHERE \"to\" = {"a@example.com"} FOR UPDATE");
            return await DeliverAsync(patience.Token);
        });

        // Assert
        taken.ShouldBe(1);
        _transport.Sent.ShouldHaveSingleItem().To.ShouldBe("b@example.com");
    }

    [Fact]
    public async Task Purge_PastTheRetention_DeletesOnlySentAndGivenUpMail()
    {
        // Arrange
        DateTimeOffset longAgo = Now - MailDelivery.Retention - TimeSpan.FromSeconds(1);
        DateTimeOffset recently = Now - MailDelivery.Retention + TimeSpan.FromSeconds(1);
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "sent-long-ago@example.com", Subject = "s", HtmlBody = "b", QueuedAt = longAgo, NextAttemptAt = longAgo, SentAt = longAgo });
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "failed-long-ago@example.com", Subject = "s", HtmlBody = "b", QueuedAt = longAgo, NextAttemptAt = longAgo, FailedAt = longAgo });
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "sent-recently@example.com", Subject = "s", HtmlBody = "b", QueuedAt = longAgo, NextAttemptAt = longAgo, SentAt = recently });
        await StoreAsync(new QueuedMail { Id = Guid.CreateVersion7(), To = "waiting@example.com", Subject = "s", HtmlBody = "b", QueuedAt = longAgo, NextAttemptAt = Now });

        // Act
        int purged;

        await using (AsyncServiceScope scope = _services.CreateAsyncScope())
        {
            purged = await scope.ServiceProvider.GetRequiredService<MailDelivery>().PurgeAsync(CancellationToken.None);
        }

        // Assert
        purged.ShouldBe(2);
        (await QueuedAsync()).Select(mail => mail.To).Order().ShouldBe(["sent-recently@example.com", "waiting@example.com"]);
    }

    [Fact]
    public async Task Worker_DeliveryEnabled_SendsWhatIsQueued()
    {
        // Arrange
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        IHostedService worker = _services.GetServices<IHostedService>().OfType<MailDeliveryWorker>().Single();

        // Act
        await worker.StartAsync(CancellationToken.None);
        OutgoingMail? sent = await _transport.FirstSent.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        sent.ShouldNotBeNull().To.ShouldBe("ana@example.com");
    }

    [Fact]
    public async Task Worker_DeliveryDisabled_StopsAtOnceAndSendsNothing()
    {
        // Arrange
        await QueueAsync(new OutgoingMail("ana@example.com", "Hello", "<p>Hi</p>"));
        var worker = new MailDeliveryWorker(
            _services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MailOptions { FromAddress = "noreply@apptemplate.test", SmtpHost = "localhost", DeliveryEnabled = false }),
            _clock,
            NullLogger<MailDeliveryWorker>.Instance);

        // Act: an enabled worker never finishes on its own, so the wait would time out.
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        _transport.Sent.ShouldBeEmpty();
    }

    private async Task QueueAsync(OutgoingMail mail)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMailQueue>().Enqueue(mail);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }

    private async Task StoreAsync(QueuedMail mail)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.QueuedMails.Add(mail);
        await db.SaveChangesAsync();
    }

    private Task<int> DeliverAsync() => DeliverAsync(CancellationToken.None);

    private async Task<int> DeliverAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MailDelivery>().DeliverDueAsync(cancellationToken);
    }

    private async Task<List<QueuedMail>> QueuedAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().QueuedMails.AsNoTracking().ToListAsync();
    }

    // Records each mail, or throws like an unreachable server; During runs while a mail is being sent.
    private sealed class RecordingTransport : IMailTransport
    {
        public List<OutgoingMail> Sent { get; } = [];

        public TaskCompletionSource<OutgoingMail> FirstSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Fails { get; set; }

        public Func<QueuedMail, Task>? During { get; set; }

        public async Task SendAsync(QueuedMail mail, CancellationToken cancellationToken)
        {
            if (During is not null)
            {
                await During(mail);
            }

            if (Fails)
            {
                throw new IOException("The mail server is unreachable.");
            }

            var sent = new OutgoingMail(mail.To, mail.Subject, mail.HtmlBody, mail.SenderName, mail.ReplyTo);
            Sent.Add(sent);
            FirstSent.TrySetResult(sent);
        }
    }
}
