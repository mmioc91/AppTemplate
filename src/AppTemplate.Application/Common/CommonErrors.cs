namespace AppTemplate.Application.Common;

// Expected failures shared by several use cases (ADR 0008). API contract: never rename or reuse a code.
public static class CommonErrors
{
    public const string ConcurrencyConflictCode = "CONCURRENCY_CONFLICT";

    // The aggregate changed after it was loaded (ADR 0013, 0023): the client reloads it and decides again.
    public static Error ConcurrencyConflict() =>
        Error.Conflict(ConcurrencyConflictCode, "The data was changed by someone else after it was loaded.");
}
