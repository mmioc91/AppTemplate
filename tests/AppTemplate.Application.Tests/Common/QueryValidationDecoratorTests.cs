using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Common;

public class QueryValidationDecoratorTests
{
    private readonly IQueryHandler<TestCommand, string> _inner = Substitute.For<IQueryHandler<TestCommand, string>>();

    public QueryValidationDecoratorTests()
    {
        _inner.HandleAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("handled"));
    }

    [Fact]
    public async Task HandleAsync_WithoutValidators_CallsInnerHandler()
    {
        // Arrange: most queries have no input to validate.
        QueryValidationDecorator<TestCommand, string> decorator = new(_inner, []);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand(null, 0), CancellationToken.None);

        // Assert
        result.Value.ShouldBe("handled");
    }

    [Fact]
    public async Task HandleAsync_ValidQuery_CallsInnerWithSameQueryAndToken()
    {
        // Arrange
        QueryValidationDecorator<TestCommand, string> decorator = new(_inner, [new NameValidator(), new CountValidator()]);
        TestCommand query = new("name", 1);
        using CancellationTokenSource cancellation = new();

        // Act
        Result<string> result = await decorator.HandleAsync(query, cancellation.Token);

        // Assert
        result.Value.ShouldBe("handled");
        await _inner.Received(1).HandleAsync(query, cancellation.Token);
    }

    [Fact]
    public async Task HandleAsync_InvalidQuery_StopsChainWithValidationError()
    {
        // Arrange
        QueryValidationDecorator<TestCommand, string> decorator = new(_inner, [new NameValidator(), new CountValidator()]);

        // Act
        Result<string> result = await decorator.HandleAsync(new TestCommand("name", 0), CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors.Keys.ShouldBe([nameof(TestCommand.Count)]);
        error.FieldErrors[nameof(TestCommand.Count)].ShouldBe(["COUNT_NOT_POSITIVE"]);
        await _inner.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }
}
