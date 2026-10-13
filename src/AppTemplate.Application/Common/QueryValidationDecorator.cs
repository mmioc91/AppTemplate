using FluentValidation;

namespace AppTemplate.Application.Common;

// The query side of ValidationDecorator: stops a query with a Validation error before the handler runs when any rule
// fails (ADR 0012, 0022). A query without a validator passes straight through.
public sealed class QueryValidationDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
{
    public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        Error? error = await InputValidation.ValidateAsync(query, validators, cancellationToken);

        return error is null ? await inner.HandleAsync(query, cancellationToken) : error;
    }
}
