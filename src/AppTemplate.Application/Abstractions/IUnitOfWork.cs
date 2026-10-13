namespace AppTemplate.Application.Abstractions;

// One command = one SaveChanges = one transaction. Implementations translate the expected outcomes so no provider
// exception leaks for them: a unique-constraint violation becomes UniqueConstraintViolationException (ADR 0013), an
// optimistic-concurrency conflict becomes ConcurrencyConflictException (ADR 0013). Anything else (e.g. a foreign-key
// violation) is a bug and surfaces as it is.
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // Forgets every loaded and changed aggregate in the scope, so the next load reads the database again — for a
    // system path that retries after a ConcurrencyConflictException or UniqueConstraintViolationException (ADR 0013).
    // Anything else tracked in the scope is dropped too.
    void DiscardChanges();
}
