using AppTemplate.Application.Common;

namespace AppTemplate.Application.Tests.Samples;

public sealed record RenameSample(string Name);

internal sealed class RenameSampleHandler(ISampleStore store) : ICommandHandler<RenameSample>
{
    public Task<Result> HandleAsync(RenameSample command, CancellationToken cancellationToken)
    {
        store.Add(command.Name);
        return Task.FromResult(Result.Success());
    }
}
