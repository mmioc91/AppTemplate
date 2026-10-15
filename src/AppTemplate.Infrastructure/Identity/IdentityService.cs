using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppTemplate.Infrastructure.Identity;

// The identity port on ASP.NET Core Identity's UserManager (ADR 0031). No SignInManager: it drags in the HTTP context
// and cookie schemes, and the only part we need — lockout-aware password checking — is a few lines. UserManager takes
// no CancellationToken (it reads its own, which is CancellationToken.None without an HTTP context), so the port's
// tokens only reach our own queries here; that is Identity's shape, not an oversight.
internal sealed class IdentityService(UserManager<ApplicationUser> users, AppDbContext db) : IIdentityService
{
    // Lockout: this many wrong passwords in a row lock the account for this long (DependencyInjection.ConfigureIdentity).
    internal const int MaxFailedAccessAttempts = 5;
    internal static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // A hash of a password nobody knows, made once per process by the configured hasher (same algorithm, same cost):
    // what a refusal verifies against when there is no real hash to check, so an unknown email and a locked-out account
    // take as long as a wrong password.
    private static string? s_dummyHash;

    public async Task<CreateUserOutcome> CreateUserAsync(
        Guid id,
        string email,
        string password,
        string displayName,
        string? role,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = new() { Id = id, UserName = email, Email = email, DisplayName = displayName };

        try
        {
            // User and role row in one transaction, inside the retrying strategy as EF requires.
            IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => CreateInTransactionAsync(user, password, role, cancellationToken));
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // The race Identity's own "email taken?" check can't see, or a retried commit of our own row: decide by
            // the database, not the constraint name (ADR 0013).
            db.Entry(user).State = EntityState.Detached;

            if (await db.Users.AsNoTracking().AnyAsync(existing => existing.Id == id, cancellationToken))
            {
                return CreateUserOutcome.Created;
            }

            if (await users.FindByEmailAsync(email) is not null)
            {
                return CreateUserOutcome.EmailTaken;
            }

            throw;
        }
    }

    public async Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToAccount(await users.FindByIdAsync(id.ToString()));

    // What SignInManager.CheckPasswordSignInAsync does with lockoutOnFailure — a locked-out account fails before the
    // password is even checked, a wrong password counts towards the lockout, a right one resets the count — except that
    // every refusal costs a hash, so the answer's timing says nothing about whether the account exists or is locked.
    // Not perfectly equal: a wrong password also writes its failure count, a few milliseconds against the hash.
    public async Task<UserAccount?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            VerifyAgainstDummy(password);
            return null;
        }

        if (await users.IsLockedOutAsync(user))
        {
            VerifyAgainstDummy(password);
            return null;
        }

        if (await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return ToAccount(user);
        }

        await users.AccessFailedAsync(user);

        return null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByIdAsync(userId.ToString());

        return user is null ? [] : (await users.GetRolesAsync(user)).ToList();
    }

    private async Task<CreateUserOutcome> CreateInTransactionAsync(
        ApplicationUser user,
        string password,
        string? role,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult created = await users.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            return Refusal(created);
        }

        if (role is not null)
        {
            IdentityResult assigned = await users.AddToRoleAsync(user, role);

            if (!assigned.Succeeded)
            {
                throw Unexpected(assigned);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return CreateUserOutcome.Created;
    }

    private void VerifyAgainstDummy(string password)
    {
        ApplicationUser nobody = new();
        s_dummyHash ??= users.PasswordHasher.HashPassword(nobody, Guid.CreateVersion7().ToString());
        users.PasswordHasher.VerifyHashedPassword(nobody, s_dummyHash, password);
    }

    // Identity reports every failed rule; the shape problems come first, like the validator's would have.
    private static CreateUserOutcome Refusal(IdentityResult result)
    {
        var codes = result.Errors.Select(error => error.Code).ToHashSet();

        if (codes.Remove(nameof(IdentityErrorDescriber.PasswordTooShort)))
        {
            return CreateUserOutcome.PasswordTooShort;
        }

        if (codes.Remove(nameof(IdentityErrorDescriber.InvalidEmail)) | codes.Remove(nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            return CreateUserOutcome.EmailInvalid;
        }

        if (codes.Remove(nameof(IdentityErrorDescriber.DuplicateEmail)) | codes.Remove(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return CreateUserOutcome.EmailTaken;
        }

        throw Unexpected(result);
    }

    // Any rule we didn't configure (password composition, user-name characters) means code and configuration drifted.
    // Codes only — a description can contain the email.
    private static InvalidOperationException Unexpected(IdentityResult result) =>
        new($"Identity refused the user: {string.Join(", ", result.Errors.Select(error => error.Code))}.");

    private static UserAccount? ToAccount(ApplicationUser? user) =>
        user is null ? null : new UserAccount(user.Id, user.Email!, user.DisplayName);
}
