using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Integration.Tests.Persistence;

// Counts the commands one DbContext sends to the database, from EF Core's diagnostic events: how a test sees into how
// many queries a load is split (an aggregate with several owned collections loads with AsSplitQuery()).
internal sealed class CommandCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly DbContext _context;
    private readonly List<IDisposable> _subscriptions = [];
    private int _count;

    public CommandCounter(DbContext context)
    {
        _context = context;
        IDisposable listeners = DiagnosticListener.AllListeners.Subscribe(this);

        lock (_subscriptions)
        {
            _subscriptions.Add(listeners);
        }
    }

    public int Count => Volatile.Read(ref _count);

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name != DbLoggerCategory.Name)
        {
            return;
        }

        lock (_subscriptions)
        {
            _subscriptions.Add(value.Subscribe(this));
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == RelationalEventId.CommandExecuted.Name && value.Value is CommandExecutedEventData data && data.Context == _context)
        {
            Interlocked.Increment(ref _count);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        lock (_subscriptions)
        {
            _subscriptions.ForEach(subscription => subscription.Dispose());
            _subscriptions.Clear();
        }
    }
}
