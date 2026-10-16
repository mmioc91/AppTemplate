using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Infrastructure.Identity;

// Access tokens are HMAC-SHA256 JWTs; refresh tokens are 256 random bits whose SHA-256 hash is the only thing stored
// (ADR 0031). Every write is saved here: a reuse must be recorded even though the use case fails.
internal sealed class TokenService(AppDbContext db, IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private const int RefreshTokenBytes = 32;

    // A family revoke or a rotation that keeps losing to concurrent writers is a bug, not something to loop on forever.
    private const int MaxWriteAttempts = 3;

    // How long after a rotation the spent token may come back from the same client — a page reloaded before it stored
    // the new pair. Short, so a stolen copy has little to work with.
    internal static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken IssueAccessToken(UserAccount user, IReadOnlyList<string> roles)
    {
        JwtOptions jwt = options.Value;
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset expires = now + jwt.AccessTokenLifetime;

        List<Claim> claims =
        [
            new(JwtClaims.Subject, user.Id.ToString()),
            new(JwtClaims.Email, user.Email),
            new(JwtClaims.Name, user.DisplayName),
            new(JwtClaims.TokenId, Guid.CreateVersion7().ToString()),
        ];
        claims.AddRange(roles.Select(role => new Claim(JwtClaims.Role, role)));

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwt.GetSigningKey()), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(Handler.CreateToken(descriptor), expires);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        // A sign-in clears the user's expired rows, those of families nobody refreshes any more included.
        await PurgeExpiredAsync(db.RefreshTokens.Where(token => token.UserId == userId), now, cancellationToken);

        (StoredRefreshToken stored, string raw) = NewToken(userId, familyId: Guid.CreateVersion7(), now);
        db.RefreshTokens.Add(stored);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // A retried commit after a lost acknowledgement inserts the row it already saved (ADR 0013): if our row is
            // there, the token is stored and the client may have it.
            db.Entry(stored).State = EntityState.Detached;

            if (!await db.RefreshTokens.AsNoTracking().AnyAsync(token => token.Id == stored.Id, cancellationToken))
            {
                throw;
            }
        }

        return new RefreshToken(raw, stored.ExpiresAt);
    }

    public async Task<RefreshTokenRotation?> RotateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            Rotation outcome = await TryRotateAsync(refreshToken, cancellationToken);

            // Another request rotated or revoked a token of this family between our read and our save (xmin). Read it
            // again: the token is then spent, and the reuse rules decide exactly as for a token presented a moment
            // later — the grace for the same client, a dead family for a copy.
            if (!outcome.LostRace || attempt == MaxWriteAttempts)
            {
                return outcome.Result;
            }

            ForgetTrackedTokens();
        }
    }

    private async Task<Rotation> TryRotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        StoredRefreshToken? presented = await FindAsync(refreshToken, cancellationToken);

        if (presented is null)
        {
            return Rotation.Refused;
        }

        DateTimeOffset now = time.GetUtcNow();

        // A token that was already rotated or revoked and shows up again: someone holds a copy, and its family is dead —
        // unless it is the same client whose answer was lost: rotated moments ago, its replacement never used. That
        // client never got the replacement, so the replacement is retired and a new one issued in its place.
        if (presented.IsRevoked)
        {
            StoredRefreshToken? unused = await UnusedReplacementAsync(presented, now, cancellationToken);

            if (unused is null)
            {
                await RevokeFamilyAsync(presented.FamilyId, now, cancellationToken);
                return Rotation.Refused;
            }

            return await RotateAsync(unused, now, cancellationToken);
        }

        if (presented.IsExpiredAt(now))
        {
            return Rotation.Refused;
        }

        return await RotateAsync(presented, now, cancellationToken);
    }

    // The token that replaced a spent one, while the grace after the rotation lasts and nobody has used it yet.
    private async Task<StoredRefreshToken?> UnusedReplacementAsync(StoredRefreshToken spent, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (spent.ReplacedById is not { } replacementId || now - spent.RevokedAt > ReuseGrace)
        {
            return null;
        }

        StoredRefreshToken? replacement = await db.RefreshTokens.SingleOrDefaultAsync(token => token.Id == replacementId, cancellationToken);
        return replacement is { IsRevoked: false } && !replacement.IsExpiredAt(now) ? replacement : null;
    }

    // The token spent and a new one of its family in its place, in one save; then the family's expired rows go.
    private async Task<Rotation> RotateAsync(StoredRefreshToken presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (StoredRefreshToken replacement, string raw) = NewToken(presented.UserId, presented.FamilyId, now);
        presented.RevokedAt = now;
        presented.ReplacedById = replacement.Id;
        db.RefreshTokens.Add(replacement);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Rotation.Lost;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // A retried commit re-inserting the replacement it already saved: the rotation happened, but this answer
            // can't carry it, so the client signs in again (ADR 0031).
            return Rotation.Refused;
        }

        await PurgeExpiredAsync(db.RefreshTokens.Where(token => token.FamilyId == presented.FamilyId), now, cancellationToken);

        return new Rotation(new RefreshTokenRotation(presented.UserId, new RefreshToken(raw, replacement.ExpiresAt)), LostRace: false);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        StoredRefreshToken? presented = await FindAsync(refreshToken, cancellationToken);

        // Someone else's token is not this user's to revoke; an unknown one is already gone.
        if (presented is null || presented.UserId != userId)
        {
            return;
        }

        await RevokeFamilyAsync(presented.FamilyId, time.GetUtcNow(), cancellationToken);
    }

    private Task<StoredRefreshToken?> FindAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string hash = Hash(refreshToken);
        return db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
    }

    // Another writer may touch the family between the query and the save — a refresh rotating one of its tokens, a
    // second replay of the same leaked token. Then xmin rejects the save: reload what changed and go again, so the
    // replacement the other writer added dies too and the caller keeps its answer (204, 401) instead of a 500.
    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            List<StoredRefreshToken> live = await db.RefreshTokens
                .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (StoredRefreshToken token in live)
            {
                token.RevokedAt = now;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException exception) when (attempt < MaxWriteAttempts)
            {
                foreach (EntityEntry entry in exception.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }
    }

    // Rows past their own expiry: a spent one there can no longer be told from an unknown token (both answer 401), and
    // a live one there is refused anyway, so keeping them only grows the table. One statement over an index (the
    // user's or the family's rows), not an aggregate: no domain rule or concurrency check applies (ADR 0031).
    private static Task<int> PurgeExpiredAsync(IQueryable<StoredRefreshToken> rows, DateTimeOffset now, CancellationToken cancellationToken) =>
        rows.Where(token => token.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

    // After a lost race the tracked rows hold what this request read and meant to write; the next read must see the
    // database. Only refresh tokens: nothing else of the scope is this port's to drop.
    private void ForgetTrackedTokens()
    {
        foreach (EntityEntry<StoredRefreshToken> entry in db.ChangeTracker.Entries<StoredRefreshToken>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private (StoredRefreshToken Stored, string Raw) NewToken(Guid userId, Guid familyId, DateTimeOffset now)
    {
        string raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

        StoredRefreshToken stored = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = Hash(raw),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now + options.Value.RefreshTokenLifetime,
        };

        return (stored, raw);
    }

    // Internal so a test can prove the table holds the hash and not the value.
    internal static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    // What one attempt came to: a rotation, a refusal (null), or a lost race to read again.
    private sealed record Rotation(RefreshTokenRotation? Result, bool LostRace)
    {
        public static readonly Rotation Refused = new(null, LostRace: false);

        public static readonly Rotation Lost = new(null, LostRace: true);
    }
}
