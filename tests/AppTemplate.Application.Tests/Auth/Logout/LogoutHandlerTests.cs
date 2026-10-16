using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth.Logout;
using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Auth.Logout;

public class LogoutHandlerTests
{
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task HandleAsync_AnyToken_RevokesItForTheSignedInUser()
    {
        // Arrange
        _currentUser.UserId.Returns(AuthSamples.User.Id);
        using CancellationTokenSource cancellation = new();
        LogoutHandler handler = new(_tokens, _currentUser);

        // Act
        Result result = await handler.HandleAsync(new LogoutCommand("presented-token"), cancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _tokens.Received(1).RevokeRefreshTokenAsync("presented-token", AuthSamples.User.Id, cancellation.Token);
    }
}
