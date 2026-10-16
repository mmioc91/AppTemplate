namespace AppTemplate.Infrastructure.Identity;

// One refresh token in the refresh_tokens table (ADR 0031): only the SHA-256 hash of the value the client holds.
// Tokens form families: a rotation revokes the presented token and links its replacement; a revoked token presented
// again revokes the whole family.
internal sealed class StoredRefreshToken
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    // Lowercase hex of the SHA-256 hash; the raw token is never stored.
    public string TokenHash { get; init; } = string.Empty;

    public Guid FamilyId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpiredAt(DateTimeOffset instant) => ExpiresAt <= instant;
}
