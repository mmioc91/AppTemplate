namespace AppTemplate.Application.Common;

// A use case that changes state and returns nothing but its outcome (ADR 0011). Resolved from DI, wrapped by
// decorators such as ValidationDecorator.
public interface ICommandHandler<in TCommand>
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
