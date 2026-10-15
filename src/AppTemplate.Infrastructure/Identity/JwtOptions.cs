using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace AppTemplate.Infrastructure.Identity;

// Bound in the Api's Program.cs with ValidateOnStart (ADR 0031). Issuer, audience and lifetimes may live in
// appsettings.json; the signing key comes from user-secrets locally and the environment in production, never from a
// committed file (root CLAUDE.md → Commands).
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 wants a key at least as long as its output.
    public const int MinSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // Base64 of at least MinSigningKeyBytes random bytes.
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 14;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    // False when the key is not base64 or decodes to fewer than MinSigningKeyBytes bytes.
    public bool TryGetSigningKey([NotNullWhen(true)] out byte[]? key)
    {
        byte[] buffer = new byte[SigningKey.Length];

        if (!Convert.TryFromBase64String(SigningKey, buffer, out int length) || length < MinSigningKeyBytes)
        {
            key = null;
            return false;
        }

        key = buffer[..length];
        return true;
    }

    public byte[] GetSigningKey() =>
        TryGetSigningKey(out byte[]? key)
            ? key
            : throw new InvalidOperationException($"{SectionName}:{nameof(SigningKey)} must be base64 of at least {MinSigningKeyBytes} bytes.");
}
