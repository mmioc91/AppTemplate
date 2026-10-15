using Microsoft.IdentityModel.JsonWebTokens;

namespace AppTemplate.Infrastructure.Identity;

// The claims an access token carries (ADR 0031). The Api validates tokens with the same names, so inbound claim
// mapping to the legacy ClaimTypes URIs is off on both sides.
public static class JwtClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string TokenId = JwtRegisteredClaimNames.Jti;

    // One claim per role.
    public const string Role = "role";
}
