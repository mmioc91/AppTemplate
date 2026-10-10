namespace AppTemplate.Application.Abstractions;

// Thrown by IUnitOfWork.SaveChangesAsync when a row changed after it was loaded (optimistic concurrency, e.g. Postgres
// xmin). A user action turns it into a Conflict (409); a background process reloads the aggregate and retries (ADR 0013).
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was changed by someone else after it was loaded.", innerException);
