using Microsoft.Extensions.Logging;

namespace AppTemplate.Application.Common;

// Outermost link of the command chain — Logging → Validation → handler (ADR 0028): the use case's name, duration and
// outcome, so a rejected command shows up with its error code too.
public sealed class LoggingDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<LoggingDecorator<TCommand>> logger,
    TimeProvider timeProvider) : ICommandHandler<TCommand>
{
    public Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        UseCaseLog.RunAsync(logger, timeProvider, typeof(TCommand).Name, () => inner.HandleAsync(command, cancellationToken));
}
