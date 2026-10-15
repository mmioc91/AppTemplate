using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Integration.Tests.Persistence;

// Runs one competing write right before the first SaveChanges after Arm() — inside the window between a method's read
// and its save, where a concurrent request would land. Disarms itself first, so a save the competing write causes
// doesn't fire it again. Register it on the context under test: ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)).
internal sealed class BeforeNextSave(Func<Task> write) : SaveChangesInterceptor
{
    private bool _armed;

    public bool Fired { get; private set; }

    public void Arm() => _armed = true;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_armed)
        {
            _armed = false;
            Fired = true;
            await write();
        }

        return result;
    }
}
