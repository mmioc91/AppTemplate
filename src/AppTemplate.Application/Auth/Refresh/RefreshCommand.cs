namespace AppTemplate.Application.Auth.Refresh;

// Trades a refresh token for a new token pair; the presented token is spent either way (ADR 0031).
public sealed record RefreshCommand(string? RefreshToken);
