using Microsoft.Extensions.Logging;

namespace AppTemplate.Infrastructure.Mail;

// What delivery writes about a mail (ADR 0028, 0034): its id and the failure's type — never the recipient, which an
// SMTP error quotes.
internal static partial class MailLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail {MailId} not delivered on attempt {Attempt}: {Error}")]
    public static partial void NotDelivered(ILogger logger, Guid mailId, int attempt, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail {MailId} given up after {Attempts} attempts: {Error}")]
    public static partial void GivenUp(ILogger logger, Guid mailId, int attempts, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "A round of mail delivery failed; trying again after the pause")]
    public static partial void RoundFailed(ILogger logger, Exception exception);
}
