namespace AppTemplate.Application.Auth;

// What register, login and refresh return: a short-lived access token for the Authorization header and a one-use
// refresh token (ADR 0031). The client keeps the access token in memory and the refresh token in storage.
public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
