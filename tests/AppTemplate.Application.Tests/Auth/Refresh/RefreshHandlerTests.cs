using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Refresh;
using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Auth.Refresh;

public class RefreshHandlerTests
{
    private static readonly RefreshCommand Command = new("presented-token");

    private static readonly RefreshToken Replacement = new("replacement-token", AuthSamples.Now.AddDays(14));

    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();

    [Fact]
    public async Task HandleAsync_ValidToken_ReturnsANewAccessTokenWithTheRotatedRefreshToken()
    {
        // Arrange
        _tokens.RotateRefreshTokenAsync(Command.RefreshToken!, Arg.Any<CancellationToken>())
            .Returns(new RefreshTokenRotation(AuthSamples.User.Id, Replacement));
        _identity.FindByIdAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns(AuthSamples.User);
        _identity.GetRolesAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns([AuthSamples.Admin]);
        _tokens.IssueAccessToken(AuthSamples.User, Arg.Is<IReadOnlyList<string>>(r => r.SequenceEqual(new[] { AuthSamples.Admin })))
            .Returns(AuthSamples.Access);
        RefreshHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert: the rotation already made the replacement — no new family is started.
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new TokenPairResponse("access-token", AuthSamples.Access.ExpiresAt, "replacement-token", Replacement.ExpiresAt));
        await _tokens.DidNotReceiveWithAnyArgs().CreateRefreshTokenAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_TokenRejectedByTheStore_ReturnsInvalidRefreshToken()
    {
        // Arrange: unknown, expired, revoked or reused — the store decides, the handler answers one code for all.
        _tokens.RotateRefreshTokenAsync(Command.RefreshToken!, Arg.Any<CancellationToken>()).Returns((RefreshTokenRotation?)null);
        RefreshHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(AuthErrors.InvalidRefreshTokenCode);
        error.Kind.ShouldBe(ErrorKind.Unauthorized);
        await _identity.DidNotReceiveWithAnyArgs().FindByIdAsync(default, default);
        _tokens.DidNotReceiveWithAnyArgs().IssueAccessToken(default!, default!);
    }

    [Fact]
    public async Task HandleAsync_UserOfTheTokenNoLongerExists_ReturnsInvalidRefreshToken()
    {
        // Arrange
        _tokens.RotateRefreshTokenAsync(Command.RefreshToken!, Arg.Any<CancellationToken>())
            .Returns(new RefreshTokenRotation(AuthSamples.User.Id, Replacement));
        _identity.FindByIdAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        RefreshHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(AuthErrors.InvalidRefreshTokenCode);
        _tokens.DidNotReceiveWithAnyArgs().IssueAccessToken(default!, default!);
    }
}
