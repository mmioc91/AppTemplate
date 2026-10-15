using AppTemplate.Application.Abstractions;
using AppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Api.Auth;

internal static class AuthenticationSetup
{
    // Tolerates clock drift between the instance that signed a token and the one validating it.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    // JWT bearer validation against JwtOptions (ADR 0031), and the fallback policy: every endpoint needs a signed-in
    // user unless it says .AllowAnonymous(). Time comes from the TimeProvider so tests can expire tokens.
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, time) =>
            {
                // Claims keep the names the token carries (sub, email, name, role), not the legacy ClaimTypes URIs.
                bearer.MapInboundClaims = false;
                bearer.TimeProvider = time;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.Value.GetSigningKey()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = ClockSkew,
                    // The library's own lifetime check reads the system clock; this one reads the host's TimeProvider.
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                        IsWithinLifetime(notBefore, expires, parameters.ClockSkew, time.GetUtcNow().UtcDateTime),
                    NameClaimType = JwtClaims.Name,
                    RoleClaimType = JwtClaims.Role,
                };
            });

        // A role-based policy joins here once the project has roles (Roles, RoleSeed), named in a static class and
        // required on a route group: .AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(Roles.Admin)).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    // What the library checks, against the given instant: an expiry is required, and both bounds tolerate the skew.
    // The typed exceptions keep the handler's challenge reason (expired vs. not yet valid).
    internal static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeSpan clockSkew, DateTime now)
    {
        if (expires is null)
        {
            throw new SecurityTokenNoExpirationException("The token has no expiration.");
        }

        if (notBefore is not null && notBefore.Value > now + clockSkew)
        {
            throw new SecurityTokenNotYetValidException("The token is not valid yet.");
        }

        if (expires.Value < now - clockSkew)
        {
            throw new SecurityTokenExpiredException("The token has expired.");
        }

        return true;
    }
}
