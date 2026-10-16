using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Register;
using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Auth.Register;

public class RegisterHandlerTests
{
    private static readonly RegisterCommand Command = new("ana@example.com", "correct horse battery", "Ana");

    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();

    [Fact]
    public async Task HandleAsync_NewEmail_CreatesAPlainAccountAndReturnsTheTokenPair()
    {
        // Arrange: no role — register makes a plain account (ADR 0031).
        Guid? createdId = null;
        _identity.CreateUserAsync(Arg.Do<Guid>(id => createdId = id), Command.Email!, Command.Password!, "Ana", null, Arg.Any<CancellationToken>())
            .Returns(CreateUserOutcome.Created);
        _identity.GetRolesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _tokens.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<IReadOnlyList<string>>()).Returns(AuthSamples.Access);
        _tokens.CreateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(AuthSamples.Refresh);
        RegisterHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new TokenPairResponse("access-token", AuthSamples.Access.ExpiresAt, "refresh-token", AuthSamples.Refresh.ExpiresAt));
        Guid id = createdId.ShouldNotBeNull();
        id.Version.ShouldBe(7);
        _tokens.Received(1).IssueAccessToken(new UserAccount(id, "ana@example.com", "Ana"), Arg.Is<IReadOnlyList<string>>(r => r.Count == 0));
        await _tokens.Received(1).CreateRefreshTokenAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DisplayNameWithSurroundingSpaces_IsTrimmed()
    {
        // Arrange
        _identity.CreateUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), "Ana", Arg.Is<string?>(role => role == null), Arg.Any<CancellationToken>())
            .Returns(CreateUserOutcome.Created);
        _tokens.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<IReadOnlyList<string>>()).Returns(AuthSamples.Access);
        _tokens.CreateRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(AuthSamples.Refresh);
        RegisterHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command with { DisplayName = "  Ana  " }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _tokens.Received(1).IssueAccessToken(Arg.Is<UserAccount>(u => u.DisplayName == "Ana"), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task HandleAsync_EmailTaken_ReturnsConflictWithoutIssuingTokens()
    {
        // Arrange
        _identity.CreateUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CreateUserOutcome.EmailTaken);
        RegisterHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(AuthErrors.EmailTakenCode);
        error.Kind.ShouldBe(ErrorKind.Conflict);
        _tokens.DidNotReceiveWithAnyArgs().IssueAccessToken(default!, default!);
        await _tokens.DidNotReceiveWithAnyArgs().CreateRefreshTokenAsync(default, default);
    }

    [Theory]
    [InlineData(CreateUserOutcome.PasswordTooShort, nameof(RegisterCommand.Password), ValidationCodes.TooShort)]
    [InlineData(CreateUserOutcome.EmailInvalid, nameof(RegisterCommand.Email), ValidationCodes.Invalid)]
    public async Task HandleAsync_IdentityRefusesTheInput_ReturnsAValidationErrorOnThatField(CreateUserOutcome outcome, string field, string code)
    {
        // Arrange: Identity's own rules (minimum password length, its email format) are reported like the validator's.
        _identity.CreateUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(outcome);
        RegisterHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors.Keys.ShouldBe([field]);
        error.FieldErrors[field].ShouldBe([code]);
        _tokens.DidNotReceiveWithAnyArgs().IssueAccessToken(default!, default!);
    }
}
