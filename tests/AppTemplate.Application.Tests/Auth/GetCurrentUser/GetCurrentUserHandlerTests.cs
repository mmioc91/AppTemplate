using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Application.Auth.GetCurrentUser;
using AppTemplate.Application.Common;
using NSubstitute;
using Shouldly;

namespace AppTemplate.Application.Tests.Auth.GetCurrentUser;

public class GetCurrentUserHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task HandleAsync_SignedInUser_ReturnsTheStoredAccountAndRoles()
    {
        // Arrange
        _currentUser.UserId.Returns(AuthSamples.User.Id);
        _identity.FindByIdAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns(AuthSamples.User);
        _identity.GetRolesAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns([AuthSamples.Admin]);
        GetCurrentUserHandler handler = new(_identity, _currentUser);

        // Act
        Result<CurrentUserResponse> result = await handler.HandleAsync(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(AuthSamples.User.Id);
        result.Value.Email.ShouldBe("ana@example.com");
        result.Value.DisplayName.ShouldBe("Ana");
        result.Value.Roles.ShouldBe([AuthSamples.Admin]);
    }

    [Fact]
    public async Task HandleAsync_UserDeletedAfterTheTokenWasIssued_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns(AuthSamples.User.Id);
        _identity.FindByIdAsync(AuthSamples.User.Id, Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        GetCurrentUserHandler handler = new(_identity, _currentUser);

        // Act
        Result<CurrentUserResponse> result = await handler.HandleAsync(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        Error error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(AuthErrors.UserNotFoundCode);
        error.Kind.ShouldBe(ErrorKind.NotFound);
    }
}
