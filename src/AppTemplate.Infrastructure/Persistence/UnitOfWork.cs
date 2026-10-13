using AppTemplate.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppTemplate.Infrastructure.Persistence;

// Never leaks provider exceptions (ADR 0013).
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgres)
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName ?? "unknown", exception);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}
