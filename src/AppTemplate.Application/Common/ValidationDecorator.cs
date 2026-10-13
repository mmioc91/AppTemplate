using FluentValidation;

namespace AppTemplate.Application.Common;

// Inside the logging decorator, around the handler: stops the chain with a Validation error instead of calling the
// handler when any rule fails (ADR 0012).
public sealed class ValidationDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand>
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        Error? error = await InputValidation.ValidateAsync(command, validators, cancellationToken);

        return error ?? await inner.HandleAsync(command, cancellationToken);
    }
}
