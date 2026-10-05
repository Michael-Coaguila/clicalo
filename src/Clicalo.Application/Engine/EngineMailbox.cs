using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Engine;

/// <summary>
/// The engine's two-lane mailbox (blueprint §3.2, rule 3): many writers, one reader (the engine thread).
/// <see cref="EngineLane.Priority"/> is always drained before <see cref="EngineLane.Normal"/>, so «Release all», the end
/// of a contact and terminal events never wait behind a macro.
/// </summary>
public sealed class EngineMailbox : IDisposable
{
    private readonly ConcurrentQueue<EngineEvent> _priority = new();
    private readonly ConcurrentQueue<EngineEvent> _normal = new();
    private readonly SemaphoreSlim _signal = new(0);
    private int _completed;
    private int _disposed;

    /// <summary>Events waiting in both lanes.</summary>
    public int Count => _priority.Count + _normal.Count;

    /// <summary>Whether <see cref="Complete"/> was called.</summary>
    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    /// <summary>Queues an event in its lane; never blocks. <see langword="false"/> after <see cref="Complete"/>.</summary>
    /// <param name="engineEvent">The event.</param>
    public bool Post(EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);
        if (IsCompleted)
        {
            return false;
        }

        (engineEvent.Lane == EngineLane.Priority ? _priority : _normal).Enqueue(engineEvent);
        Wake();
        return true;
    }

    /// <summary>Takes the next event: the oldest priority event, or else the oldest normal one.</summary>
    /// <param name="engineEvent">The event.</param>
    public bool TryTake([NotNullWhen(true)] out EngineEvent? engineEvent) =>
        _priority.TryDequeue(out engineEvent) || _normal.TryDequeue(out engineEvent);

    /// <summary>Waits until an event is queued, the timeout passes or the token is cancelled (the engine loop's only wait).</summary>
    /// <param name="timeout">Longest wait (the next timer or heartbeat).</param>
    /// <param name="cancellationToken">Stops the engine.</param>
    public bool WaitForEvent(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Count > 0)
        {
            return true;
        }

        try
        {
            _signal.Wait(timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return Count > 0;
    }

    /// <summary>Refuses further events.</summary>
    public void Complete()
    {
        Volatile.Write(ref _completed, 1);
        Wake();
    }

    /// <summary>Wakes the reader without an event (a timer fell due).</summary>
    public void Wake()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (ObjectDisposedException)
            {
                // The engine is gone; nobody waits.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _signal.Dispose();
        }
    }
}
