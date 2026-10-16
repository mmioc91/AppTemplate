using AppTemplate.Application.Auth;

namespace AppTemplate.Application.Abstractions;

// Access and refresh tokens (ADR 0031). Refresh tokens are stored hashed and rotated: every rotation revokes the
// presented token and issues a new one in the same family; a token presented twice revokes its whole family. The
// refresh-token methods persist their own writes — a reuse must be recorded even though the use case fails.
public interface ITokenService
{
    AccessToken IssueAccessToken(UserAccount user, IReadOnlyList<string> roles);

    // A new family for a fresh sign-in.
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken);

    // Null when the token is unknown, expired, revoked or reused; the use case never learns which.
    Task<RefreshTokenRotation?> RotateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);

    // Revokes the token's family when it belongs to the user; otherwise does nothing.
    Task RevokeRefreshTokenAsync(string refreshToken, Guid userId, CancellationToken cancellationToken);
}
