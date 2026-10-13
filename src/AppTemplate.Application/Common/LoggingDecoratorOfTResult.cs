using Microsoft.Extensions.Logging;

namespace AppTemplate.Application.Common;

// Outermost link of the command chain — Logging → Validation → handler (ADR 0028): the use case's name, duration and
// outcome, so a rejected command shows up with its error code too.
public sealed class LoggingDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggingDecorator<TCommand, TResult>> logger,
    TimeProvider timeProvider) : ICommandHandler<TCommand, TResult>
{
    public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        UseCaseLog.RunAsync(logger, timeProvider, typeof(TCommand).Name, () => inner.HandleAsync(command, cancellationToken));
}
