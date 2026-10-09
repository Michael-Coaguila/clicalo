using System.Collections.Concurrent;

namespace Clicalo.Platform.Windows.Launch;

/// <summary>
/// The Shell thread (blueprint §3.2): an STA thread with BelowNormal priority that runs launches and system commands
/// one after another, so a slow <c>ShellExecute</c> or WMI call never delays the engine or the SysEvents thread. It never
/// sends input and never touches the UI. Work that throws is reported and the thread goes on.
/// </summary>
internal sealed class ShellThread : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly Action<Exception>? _onFailure;
    private int _disposed;

    /// <summary>Starts the thread.</summary>
    /// <param name="onFailure">Hears a failure of a work item (the caller logs its type); never throws.</param>
    public ShellThread(Action<Exception>? onFailure = null)
    {
        _onFailure = onFailure;
        _thread = new Thread(Run)
        {
            Name = "Clicalo.Shell",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Queues <paramref name="work"/>; it is dropped when the thread has ended.</summary>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _work.Add(work);
        }
        catch (InvalidOperationException)
        {
            // Completed while posting: the process is ending.
        }
    }

    /// <summary>Lets the queued work finish and ends the thread.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _work.CompleteAdding();
        if (Environment.CurrentManagedThreadId != _thread.ManagedThreadId)
        {
            _ = _thread.Join(TimeSpan.FromSeconds(2));
        }

        _work.Dispose();
    }

    private void Run()
    {
        foreach (var work in _work.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                try
                {
                    _onFailure?.Invoke(ex);
                }
                catch (Exception)
                {
                    // A failing observer never ends the thread.
                }
            }
        }
    }
}
