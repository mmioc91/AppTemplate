using AppTemplate.Api.Auth;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api.Auth;

// The lifetime check the bearer handler runs on the host's clock instead of the system clock (ADR 0031): no server
// needed, the rule is a pure function of the instants.
public class AuthenticationSetupTests
{
    private static readonly DateTime Now = new(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(600)]
    [InlineData(0)]
    [InlineData(-29)]
    public void IsWithinLifetime_ExpiryNotPastTheSkew_IsTrue(int expiresInSeconds)
    {
        // Act
        bool valid = AuthenticationSetup.IsWithinLifetime(Now.AddMinutes(-15), Now.AddSeconds(expiresInSeconds), Skew, Now);

        // Assert
        valid.ShouldBeTrue();
    }

    [Fact]
    public void IsWithinLifetime_ExpiryPastTheSkew_ThrowsExpired()
    {
        // Act
        Action act = () => AuthenticationSetup.IsWithinLifetime(Now.AddMinutes(-15), Now.AddSeconds(-31), Skew, Now);

        // Assert: the typed exception keeps the handler's challenge reason.
        Should.Throw<SecurityTokenExpiredException>(act);
    }

    [Fact]
    public void IsWithinLifetime_NotBeforeInsideTheSkew_IsTrue()
    {
        // Act
        bool valid = AuthenticationSetup.IsWithinLifetime(Now.AddSeconds(29), Now.AddMinutes(15), Skew, Now);

        // Assert
        valid.ShouldBeTrue();
    }

    [Fact]
    public void IsWithinLifetime_NotBeforePastTheSkew_ThrowsNotYetValid()
    {
        // Act
        Action act = () => AuthenticationSetup.IsWithinLifetime(Now.AddSeconds(31), Now.AddMinutes(15), Skew, Now);

        // Assert
        Should.Throw<SecurityTokenNotYetValidException>(act);
    }

    [Fact]
    public void IsWithinLifetime_WithoutAnExpiry_ThrowsNoExpiration()
    {
        // Act
        Action act = () => AuthenticationSetup.IsWithinLifetime(null, null, Skew, Now);

        // Assert: a token that never expires is never ours.
        Should.Throw<SecurityTokenNoExpirationException>(act);
    }
}
