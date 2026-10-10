namespace AppTemplate.Application.Common;

// A use case that only reads (ADR 0011): projects straight to a DTO through a read store — no repository, no
// transaction. Registered by contract only, like command handlers; a query with input gets a validation decorator
// when the first one needs it.
public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
