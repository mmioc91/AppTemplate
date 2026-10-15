namespace AppTemplate.Application.Abstractions;

// The raw refresh token, handed to the client once; only its hash is stored.
public sealed record RefreshToken(string Token, DateTimeOffset ExpiresAt);
