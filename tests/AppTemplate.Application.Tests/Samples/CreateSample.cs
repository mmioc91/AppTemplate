using AppTemplate.Application.Common;
using FluentValidation;

namespace AppTemplate.Application.Tests.Samples;

// Sample use cases of every kind, registered by AddUseCases exactly like the ones in Application (DependencyInjectionTests).
public sealed record CreateSample(string? Name);

internal sealed class CreateSampleValidator : AbstractValidator<CreateSample>
{
    public CreateSampleValidator() => RuleFor(c => c.Name).NotEmpty().WithErrorCode(ValidationCodes.Required);
}

internal sealed class CreateSampleHandler(ISampleStore store) : ICommandHandler<CreateSample, Guid>
{
    public Task<Result<Guid>> HandleAsync(CreateSample command, CancellationToken cancellationToken)
    {
        store.Add(command.Name!);
        return Task.FromResult<Result<Guid>>(Guid.CreateVersion7());
    }
}
