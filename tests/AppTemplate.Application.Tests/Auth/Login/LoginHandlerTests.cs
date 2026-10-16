using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.Login;
using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Auth.Login;

public class LoginHandlerTests
{
    private static readonly LoginCommand Command = new(AuthSamples.User.Email, "correct horse battery");

    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = AuthSamples.TokenServiceIssuing(AuthSamples.User, [AuthSamples.Admin, AuthSamples.Editor]);

    [Fact]
    public async Task HandleAsync_RightPassword_ReturnsATokenPairForTheUsersRoles()
    {
        // Arrange
        _identity.CheckPasswordAsync(Command.Email!, Command.Password!, Arg.Any<CancellationToken>()).Returns(AuthSamples.User);
        _identity.GetRolesAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns([AuthSamples.Admin, AuthSamples.Editor]);
        LoginHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new TokenPairResponse("access-token", AuthSamples.Access.ExpiresAt, "refresh-token", AuthSamples.Refresh.ExpiresAt));
    }

    [Fact]
    public async Task HandleAsync_CredentialsRefused_ReturnsInvalidCredentialsWithoutIssuingTokens()
    {
        // Arrange: an unknown email, a wrong password and a lockout all come back as null from the port, and all get
        // the same code (ADR 0031).
        _identity.CheckPasswordAsync(Command.Email!, Command.Password!, Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        LoginHandler handler = new(_identity, _tokens);

        // Act
        Result<TokenPairResponse> result = await handler.HandleAsync(Command, CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(AuthErrors.InvalidCredentialsCode);
        error.Kind.ShouldBe(ErrorKind.Unauthorized);
        _tokens.DidNotReceiveWithAnyArgs().IssueAccessToken(default!, default!);
        await _tokens.DidNotReceiveWithAnyArgs().CreateRefreshTokenAsync(default, default);
    }
}
