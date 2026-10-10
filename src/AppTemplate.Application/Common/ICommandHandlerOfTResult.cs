namespace AppTemplate.Application.Common;

// A use case that changes state and returns a value, e.g. the new id (ADR 0011). Resolved from DI, wrapped by
// decorators such as ValidationDecorator.
public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
