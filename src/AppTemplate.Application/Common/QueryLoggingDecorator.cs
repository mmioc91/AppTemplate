using Microsoft.Extensions.Logging;

namespace AppTemplate.Application.Common;

// The query side of LoggingDecorator: Logging → Validation → handler (ADR 0028).
public sealed class QueryLoggingDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<QueryLoggingDecorator<TQuery, TResult>> logger,
    TimeProvider timeProvider) : IQueryHandler<TQuery, TResult>
{
    public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        UseCaseLog.RunAsync(logger, timeProvider, typeof(TQuery).Name, () => inner.HandleAsync(query, cancellationToken));
}
