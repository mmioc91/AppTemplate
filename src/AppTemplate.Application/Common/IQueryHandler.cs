namespace AppTemplate.Application.Common;

// A use case that only reads (ADR 0011): projects straight to a DTO through a read store — no repository, no
// transaction. Registered by contract only and wrapped in QueryValidationDecorator, like command handlers (ADR 0022).
public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
