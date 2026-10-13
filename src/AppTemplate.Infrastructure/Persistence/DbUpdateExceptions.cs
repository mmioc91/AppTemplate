using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppTemplate.Infrastructure.Persistence;

// For Infrastructure code that saves its own rows outside the unit of work (an identity service, a worker): the same
// translation UnitOfWork does, as a question.
internal static class DbUpdateExceptions
{
    // A Postgres unique violation (SQLSTATE 23505) behind EF's exception: the race a pre-check can't see, or a
    // retried commit re-inserting the row it already saved (ADR 0013).
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
