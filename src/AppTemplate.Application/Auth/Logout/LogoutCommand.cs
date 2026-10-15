namespace AppTemplate.Application.Auth.Logout;

// Revokes the client's refresh token family. The access token stays valid until it expires — that is why it is short.
public sealed record LogoutCommand(string? RefreshToken);
