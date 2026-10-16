namespace AppTemplate.Application.Auth;

// A user as the identity port reports it, never the Identity entity itself. Aggregates refer to a user by Id only.
public sealed record UserAccount(Guid Id, string Email, string DisplayName);
