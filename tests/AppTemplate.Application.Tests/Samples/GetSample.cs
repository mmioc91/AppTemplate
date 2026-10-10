using AppTemplate.Application.Common;

namespace AppTemplate.Application.Tests.Samples;

public sealed record GetSample(Guid Id);

internal sealed class GetSampleHandler : IQueryHandler<GetSample, string>
{
    public Task<Result<string>> HandleAsync(GetSample query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<string>>(query.Id.ToString());
}
