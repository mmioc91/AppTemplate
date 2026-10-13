using AppTemplate.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

// The query side shares UseCaseLog with LoggingDecorator (pinned there); here only that it names the query and passes
// its answer through.
public class QueryLoggingDecoratorTests
{
    public sealed record SomeQuery(int Page);

    [Fact]
    public async Task HandleAsync_LogsTheQuerysNameAndReturnsTheInnerResult()
    {
        // Arrange
        IQueryHandler<SomeQuery, string> inner = Substitute.For<IQueryHandler<SomeQuery, string>>();
        using CancellationTokenSource cancellation = new();
        SomeQuery query = new(2);
        inner.HandleAsync(query, cancellation.Token).Returns(Result<string>.Success("page 2"));
        FakeLogger<QueryLoggingDecorator<SomeQuery, string>> logger = new();

        // Act
        Result<string> result = await new QueryLoggingDecorator<SomeQuery, string>(inner, logger, new FakeTimeProvider())
            .HandleAsync(query, cancellation.Token);

        // Assert
        result.Value.ShouldBe("page 2");
        logger.LatestRecord.Level.ShouldBe(LogLevel.Debug);
        logger.LatestRecord.GetStructuredStateValue("UseCase").ShouldBe(nameof(SomeQuery));
    }
}
