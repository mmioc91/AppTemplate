using AppTemplate.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

// What a use case leaves in the log (ADR 0028): name, duration, outcome — never its input.
public class LoggingDecoratorTests
{
    // A command carrying personal data the log must never show.
    public sealed record SecretCommand(string Street);

    private const string Street = "Ilica 1";

    private readonly ICommandHandler<SecretCommand, int> _inner = Substitute.For<ICommandHandler<SecretCommand, int>>();
    private readonly FakeLogger<LoggingDecorator<SecretCommand, int>> _logger = new();
    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public async Task HandleAsync_Success_IsLoggedAtDebugWithTheUseCaseAndDuration()
    {
        // Arrange
        _inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _clock.Advance(TimeSpan.FromMilliseconds(42));
                return Result<int>.Success(1);
            });

        // Act
        Result<int> result = await Decorator().HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert
        result.Value.ShouldBe(1);
        FakeLogRecord record = _logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Debug);
        record.GetStructuredStateValue("UseCase").ShouldBe(nameof(SecretCommand));
        record.GetStructuredStateValue("ElapsedMs").ShouldBe("42");
        record.Message.ShouldNotContain(Street);
        _logger.Collector.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_Failure_IsLoggedAtInformationWithItsCodeAndKind()
    {
        // Arrange
        var conflict = Error.Conflict("ORDER_DUPLICATE", $"Already placed from {Street}.");
        _inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>()).Returns(conflict);

        // Act
        Result<int> result = await Decorator().HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert
        result.Error.ShouldBeSameAs(conflict);
        FakeLogRecord record = _logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.GetStructuredStateValue("ErrorCode").ShouldBe("ORDER_DUPLICATE");
        record.GetStructuredStateValue("ErrorKind").ShouldBe(nameof(ErrorKind.Conflict));
    }

    [Fact]
    public async Task HandleAsync_Exception_IsLoggedByTypeAndRethrown()
    {
        // Arrange
        InvalidOperationException thrown = new($"Broken at {Street}.");
        _inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(thrown);

        // Act
        Func<Task> act = () => Decorator().HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert: the exception itself is logged where it's handled, not here.
        (await Should.ThrowAsync<InvalidOperationException>(act)).ShouldBeSameAs(thrown);
        FakeLogRecord record = _logger.LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.GetStructuredStateValue("ExceptionType").ShouldBe(nameof(InvalidOperationException));
        record.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_Cancelled_IsRethrownWithoutAnEntry()
    {
        // Arrange: the client went away; that is nobody's failure.
        _inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = () => Decorator().HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
        _logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_Failure_NeverLogsTheInputOrTheErrorMessage()
    {
        // Arrange
        _inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Conflict("ORDER_DUPLICATE", $"Already placed from {Street}."));

        // Act
        await Decorator().HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert: neither the rendered message nor any structured value carries the input.
        IReadOnlyList<FakeLogRecord> records = _logger.Collector.GetSnapshot();
        records.ShouldAllBe(r => !r.Message.Contains(Street));
        records.SelectMany(r => r.StructuredState ?? []).ShouldAllBe(pair => pair.Value == null || !pair.Value.Contains(Street));
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_PassesItAndTheTokenThrough()
    {
        // Arrange
        using CancellationTokenSource cancellation = new();
        SecretCommand command = new(Street);
        _inner.HandleAsync(command, cancellation.Token).Returns(Result<int>.Success(7));

        // Act
        Result<int> result = await Decorator().HandleAsync(command, cancellation.Token);

        // Assert
        result.Value.ShouldBe(7);
    }

    [Fact]
    public async Task HandleAsync_CommandWithoutAValue_IsLoggedTheSameWay()
    {
        // Arrange: the Result side of the chain shares UseCaseLog with the Result<T> side.
        ICommandHandler<SecretCommand> inner = Substitute.For<ICommandHandler<SecretCommand>>();
        inner.HandleAsync(Arg.Any<SecretCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(Error.NotFound("ORDER_NOT_FOUND", Street)));
        FakeLogger<LoggingDecorator<SecretCommand>> logger = new();

        // Act
        Result result = await new LoggingDecorator<SecretCommand>(inner, logger, _clock).HandleAsync(new SecretCommand(Street), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
        logger.LatestRecord.GetStructuredStateValue("UseCase").ShouldBe(nameof(SecretCommand));
        logger.LatestRecord.GetStructuredStateValue("ErrorCode").ShouldBe("ORDER_NOT_FOUND");
        logger.LatestRecord.Message.ShouldNotContain(Street);
    }

    private LoggingDecorator<SecretCommand, int> Decorator() => new(_inner, _logger, _clock);
}
