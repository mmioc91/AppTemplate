using FluentValidation;

namespace AppTemplate.Application.Common;

// First link of the handler chain: stops the chain with a Validation error instead of calling the handler when any
// rule fails (ADR 0012).
public sealed class ValidationDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
{
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        Error? error = await CommandValidation.ValidateAsync(command, validators, cancellationToken);

        return error is null ? await inner.HandleAsync(command, cancellationToken) : error;
    }
}
