using System.Security.Claims;
using AppTemplate.Api.Auth;
using AppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api.Auth;

// The ICurrentUser port on a principal shaped like the bearer identity (ADR 0031): no server needed.
public class CurrentUserTests
{
    private static readonly Guid UserId = Guid.Parse("01a1231c-0000-7000-8000-00000000aaaa");

    [Fact]
    public void IsAuthenticated_WithoutARequest_IsFalse()
    {
        // Arrange: no HTTP context at all — a background path asking who is signed in.
        CurrentUser currentUser = new(new HttpContextAccessor());

        // Act & Assert
        currentUser.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void UserId_NobodySignedIn_Throws()
    {
        // Arrange: an anonymous request has a principal without an authenticated identity.
        CurrentUser currentUser = new(Accessor(new ClaimsPrincipal(new ClaimsIdentity())));

        // Act
        Action act = () => _ = currentUser.UserId;

        // Assert: a programming error (an anonymous endpoint reaching a use case that needs a user), not an outcome.
        currentUser.IsAuthenticated.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void UserId_SignedIn_IsTheSubjectClaim()
    {
        // Arrange
        CurrentUser currentUser = new(Accessor(BearerPrincipal()));

        // Act & Assert
        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.UserId.ShouldBe(UserId);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Editor", false)]
    public void IsInRole_ReadsTheTokensRoleClaims(string role, bool expected)
    {
        // Arrange
        CurrentUser currentUser = new(Accessor(BearerPrincipal("Admin")));

        // Act
        bool inRole = currentUser.IsInRole(role);

        // Assert
        inRole.ShouldBe(expected);
    }

    // What the bearer handler produces: the token's claim names, the role claim type set to JwtClaims.Role.
    private static ClaimsPrincipal BearerPrincipal(params string[] roles)
    {
        List<Claim> claims = [new(JwtClaims.Subject, UserId.ToString())];
        claims.AddRange(roles.Select(role => new Claim(JwtClaims.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", JwtClaims.Name, JwtClaims.Role));
    }

    private static HttpContextAccessor Accessor(ClaimsPrincipal principal) =>
        new() { HttpContext = new DefaultHttpContext { User = principal } };
}
