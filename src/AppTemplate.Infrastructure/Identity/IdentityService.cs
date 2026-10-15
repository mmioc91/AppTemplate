using AppTemplate.Application.Abstractions;
using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

    // How often a write of the user row is tried again after losing to a concurrent one: as many as the guesses that
    // lock an account, so a burst of them still ends in a lockout.
    private const int MaxRowWriteAttempts = MaxFailedAccessAttempts;

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
            await ResetAccessFailedCountAsync(user, cancellationToken);
            return ToAccount(user);
        }

        await CountAccessFailedAsync(user, cancellationToken);

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
        // A retried attempt starts from what the database holds: the rolled-back attempt left the user and its role row
        // tracked as saved, and Identity would take the role row for one that exists (UserAlreadyInRole).
        ForgetTrackedRows(user);

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

    // UserManager writes the user row with its concurrency stamp and reports a lost race as a ConcurrencyFailure it
    // never throws: another request (a second submit, a guess at the same account) wrote the row since it was read. The
    // entry then stays Modified with the stale stamp, and the next SaveChanges in this scope — the refresh token's —
    // would replay it and fail. So a lost race reloads the row and goes again; the rehash UserManager.CheckPasswordAsync
    // may have attempted on its own is covered by the same reload.
    private async Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRowWriteAttempts; attempt++)
        {
            if ((await users.ResetAccessFailedCountAsync(user)).Succeeded && IsSaved(user))
            {
                return;
            }

            await db.Entry(user).ReloadAsync(cancellationToken);
        }

        // Still losing: leave the count for the next sign-in rather than fail this one, and nothing stale behind.
        await DiscardChangesAsync(user, cancellationToken);
    }

    // Every wrong password counts, also when guesses at one account arrive together: an increment that lost the race
    // is made again on the fresh row, until it lands or the account is locked out anyway.
    private async Task CountAccessFailedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRowWriteAttempts; attempt++)
        {
            if ((await users.AccessFailedAsync(user)).Succeeded)
            {
                return;
            }

            await db.Entry(user).ReloadAsync(cancellationToken);

            if (db.Entry(user).State == EntityState.Detached || await users.IsLockedOutAsync(user))
            {
                return;
            }
        }

        await DiscardChangesAsync(user, cancellationToken);
    }

    // Only what this port stages for the user, never the rest of the scope's changes: a use case may have its own.
    private void ForgetTrackedRows(ApplicationUser user)
    {
        foreach (EntityEntry entry in db.ChangeTracker.Entries()
            .Where(entry => ReferenceEquals(entry.Entity, user) || entry.Entity is IdentityUserRole<Guid> userRole && userRole.UserId == user.Id)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private bool IsSaved(ApplicationUser user) => db.Entry(user).State == EntityState.Unchanged;

    private async Task DiscardChangesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (db.Entry(user).State == EntityState.Modified)
        {
            await db.Entry(user).ReloadAsync(cancellationToken);
        }
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
