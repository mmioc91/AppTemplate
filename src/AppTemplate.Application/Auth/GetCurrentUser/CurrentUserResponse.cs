namespace AppTemplate.Application.Auth.GetCurrentUser;

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
