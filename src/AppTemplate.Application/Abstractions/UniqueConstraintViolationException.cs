namespace AppTemplate.Application.Abstractions;

// Thrown by IUnitOfWork.SaveChangesAsync when the database rejects a write that breaks a unique constraint — the race
// a use case's own "already exists?" check can't see (two requests at once). Use cases turn it into a Conflict (ADR 0013).
public sealed class UniqueConstraintViolationException(string constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}
