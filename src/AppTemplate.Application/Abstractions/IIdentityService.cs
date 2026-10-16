using AppTemplate.Application.Auth;

namespace AppTemplate.Application.Abstractions;

// Users, passwords and roles (ADR 0031): Infrastructure implements it on ASP.NET Core Identity. Every method
// persists its own writes, as Identity does; the auth use cases don't go through IUnitOfWork.
public interface IIdentityService
{
    // Creates the user with the given id (UUIDv7 from the use case, ADR 0003) and role — none for a plain account; a
    // role must be one the database holds (Roles, RoleSeed) — or reports why Identity refused.
    Task<CreateUserOutcome> CreateUserAsync(
        Guid id,
        string email,
        string password,
        string displayName,
        string? role,
        CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    // The user these credentials belong to, or null for an unknown email, a wrong password and a locked-out account
    // alike — the caller never learns which, and neither does the clock: every refusal costs one password hash. A wrong
    // password counts towards the lockout, a right one resets the count.
    Task<UserAccount?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken);
}
