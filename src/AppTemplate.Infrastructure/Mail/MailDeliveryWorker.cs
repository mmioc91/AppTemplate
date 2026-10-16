using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Mail;

// Drains the mail queue in the background (ADR 0034): a batch at a time, straight on while batches come full, then a
// pause; old rows are purged once an hour. A failing round is logged and tried again after the pause — whatever it
// threw, the host's own stop apart: an exception that escaped here would stop the whole host (BackgroundService's
// default), and a mail server's fault must not.
internal sealed class MailDeliveryWorker(
    IServiceScopeFactory scopes,
    IOptions<MailOptions> options,
    TimeProvider time,
    ILogger<MailDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MailOptions settings = options.Value;

        if (!settings.DeliveryEnabled)
        {
            return;
        }

        DateTimeOffset nextPurge = time.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            int taken = 0;

            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                MailDelivery delivery = scope.ServiceProvider.GetRequiredService<MailDelivery>();
                taken = await delivery.DeliverDueAsync(stoppingToken);

                if (time.GetUtcNow() >= nextPurge)
                {
                    await delivery.PurgeAsync(stoppingToken);
                    nextPurge = time.GetUtcNow() + PurgeInterval;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                MailLog.RoundFailed(logger, exception);
            }

            if (taken < settings.BatchSize)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), time, stoppingToken);
            }
        }
    }
}
