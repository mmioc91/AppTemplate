namespace AppTemplate.Application.Auth.Register;

// Creates a plain account (no role) and signs it in.
public sealed record RegisterCommand(string? Email, string? Password, string? DisplayName);
