using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

// The decorator for commands without a result; the validation itself is shared with ValidationDecorator<TCommand, TResult>.
public class ValidationDecoratorTests
{
    private readonly ICommandHandler<TestCommand> _inner = Substitute.For<ICommandHandler<TestCommand>>();

    public ValidationDecoratorTests()
    {
        _inner.HandleAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CallsInnerWithSameCommandAndToken()
    {
        // Arrange
        ValidationDecorator<TestCommand> decorator = new(_inner, [new NameValidator(), new CountValidator()]);
        TestCommand command = new("name", 1);
        using CancellationTokenSource cancellation = new();

        // Act
        Result result = await decorator.HandleAsync(command, cancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _inner.Received(1).HandleAsync(command, cancellation.Token);
    }

    [Fact]
    public async Task HandleAsync_InnerFails_ReturnsItsError()
    {
        // Arrange
        var conflict = Error.Conflict("SOME_CONFLICT", "Something conflicts.");
        _inner.HandleAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(conflict));
        ValidationDecorator<TestCommand> decorator = new(_inner, [new NameValidator()]);

        // Act
        Result result = await decorator.HandleAsync(new TestCommand("name", 1), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(conflict);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_StopsChainWithValidationError()
    {
        // Arrange
        ValidationDecorator<TestCommand> decorator = new(_inner, [new NameValidator(), new CountValidator()]);

        // Act
        Result result = await decorator.HandleAsync(new TestCommand(null, 0), CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors[nameof(TestCommand.Name)].ShouldBe(["NAME_REQUIRED"]);
        error.FieldErrors[nameof(TestCommand.Count)].ShouldBe(["COUNT_NOT_POSITIVE"]);
        await _inner.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }
}
