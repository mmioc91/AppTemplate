using AppTemplate.Application.Common;
using FluentValidation;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

public class ValidationDecoratorOfTResultTests
{
    private readonly ICommandHandler<TestCommand, string> _inner = Substitute.For<ICommandHandler<TestCommand, string>>();

    public ValidationDecoratorOfTResultTests()
    {
        _inner.HandleAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("handled"));
    }

    [Fact]
    public async Task HandleAsync_WithoutValidators_CallsInnerHandler()
    {
        // Arrange
        ValidationDecorator<TestCommand, string> decorator = new(_inner, []);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand("name", 1), CancellationToken.None);

        // Assert
        result.Value.ShouldBe("handled");
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CallsInnerWithSameCommandAndToken()
    {
        // Arrange
        ValidationDecorator<TestCommand, string> decorator = new(_inner, [new NameValidator(), new CountValidator()]);
        TestCommand command = new("name", 1);
        using CancellationTokenSource cancellation = new();

        // Act
        Result<string> result = await decorator.HandleAsync(command, cancellation.Token);

        // Assert
        result.Value.ShouldBe("handled");
        await _inner.Received(1).HandleAsync(command, cancellation.Token);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_StopsChainWithValidationError()
    {
        // Arrange
        ValidationDecorator<TestCommand, string> decorator = new(_inner, [new NameValidator()]);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand(null, 1), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        Error error = result.Error!;
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe(Error.ValidationFailedCode);
        error.FieldErrors[nameof(TestCommand.Name)].ShouldBe(["NAME_REQUIRED"]);
        await _inner.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_FailuresFromSeveralValidators_ReportsEveryFailingField()
    {
        // Arrange
        ValidationDecorator<TestCommand, string> decorator = new(_inner, [new NameValidator(), new CountValidator()]);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand("", 0), CancellationToken.None);

        // Assert
        result.Error!.FieldErrors.Keys.ShouldBe([nameof(TestCommand.Name), nameof(TestCommand.Count)], ignoreOrder: true);
        result.Error.FieldErrors[nameof(TestCommand.Count)].ShouldBe(["COUNT_NOT_POSITIVE"]);
    }

    [Fact]
    public async Task HandleAsync_SeveralRulesFailOnOneField_ReportsEachCodeOnce()
    {
        // Arrange
        ValidationDecorator<TestCommand, string> decorator =
            new(_inner, [new NameValidator(), new NameValidator(), new NameLengthValidator()]);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand("", 1), CancellationToken.None);

        // Assert
        result.Error!.FieldErrors[nameof(TestCommand.Name)].ShouldBe(["NAME_REQUIRED", "NAME_TOO_SHORT"], ignoreOrder: true);
    }
}
