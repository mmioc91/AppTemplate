using System.Security.Claims;
using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Identity;

namespace AppTemplate.Api.Auth;

// The ICurrentUser port from the request's principal (ADR 0031): the access token's claims, never the body.
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(JwtClaims.Subject), out Guid id)
            ? id
            : throw new InvalidOperationException("No user is signed in; the endpoint should not be anonymous.");

    // The bearer identity's role claim type is JwtClaims.Role, so IsInRole reads the token's role claims.
    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
