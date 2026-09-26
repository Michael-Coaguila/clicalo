using System.Globalization;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>Thread-safe record of events (tile events, UI Automation events), with waiting.</summary>
/// <typeparam name="T">The recorded event.</typeparam>
public sealed class EventLog<T>
{
    private readonly Lock _gate = new();
    private readonly List<T> _entries = [];
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>How many events have been recorded.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>A copy of the events recorded after <paramref name="cursor"/>.</summary>
    public IReadOnlyList<T> Since(int cursor)
    {
        lock (_gate)
        {
            return _entries[cursor..];
        }
    }

    /// <summary>Records <paramref name="entry"/>.</summary>
    public void Record(T entry)
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            _entries.Add(entry);
            signal = _changed;
            _changed = NewSignal();
        }

        signal.TrySetResult();
    }

    /// <summary>Waits until <paramref name="count"/> events have been recorded after <paramref name="cursor"/>.</summary>
    public async Task<IReadOnlyList<T>> WaitForAsync(
        int cursor,
        int count,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_entries.Count - cursor >= count)
                {
                    return _entries[cursor..];
                }

                changed = _changed.Task;
            }

            try
            {
                await changed.WaitAsync(deadline.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Expected {count} events within {timeout.TotalMilliseconds} ms; got {Since(cursor).Count}."
                    ),
                    ex
                );
            }
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
