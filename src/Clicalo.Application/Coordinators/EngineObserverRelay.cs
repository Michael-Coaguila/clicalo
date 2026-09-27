using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// The engine's <see cref="IEngineObserver"/> (the <c>EngineOutputRouter</c> of blueprint §8.2 in its M2 form): it is
/// called on the engine thread and only queues to the Surfaces role, where its events are raised. Snapshots are
/// coalesced: however many arrive before the Surfaces thread runs, it paints only the newest one (§3.2 rule 5), so the
/// engine never waits for the UI.
/// </summary>
public sealed class EngineObserverRelay : IEngineObserver
{
    private readonly Action<Action> _toSurfaces;
    private readonly Lock _gate = new();
    private readonly List<TaskCompletionSource> _idleWaiters = [];
    private EngineSnapshot _latest = EngineSnapshot.Empty;
    private int _snapshotQueued;
    private ShortcutId? _lastAction;

    /// <summary>Creates the relay.</summary>
    /// <param name="toSurfaces">Queues work on the UI thread of the Surfaces role; never blocks.</param>
    public EngineObserverRelay(Action<Action> toSurfaces)
    {
        ArgumentNullException.ThrowIfNull(toSurfaces);
        _toSurfaces = toSurfaces;
    }

    /// <summary>A newer snapshot is available (on the Surfaces thread; the panic strip follows it, SEG-002).</summary>
    public event EventHandler<EngineSnapshotEventArgs>? SnapshotChanged;

    /// <summary>A notice for the panel and screen readers (on the Surfaces thread).</summary>
    public event EventHandler<EngineNoticeEventArgs>? NoticeRaised;

    /// <summary>
    /// A shortcut ran effectively (on the Surfaces thread); the composition dispatches the <c>RecordUsage</c> document
    /// command for Frequents (FRE-002).
    /// </summary>
    public event EventHandler<UsageCountedEventArgs>? UsageCounted;

    /// <summary>The newest snapshot the engine published; safe to read from any thread.</summary>
    public EngineSnapshot Latest => Volatile.Read(ref _latest);

    /// <summary>The last shortcut that ran, for Repeat (AVI-004, M3).</summary>
    public ShortcutId? LastAction
    {
        get
        {
            lock (_gate)
            {
                return _lastAction;
            }
        }
    }

    /// <inheritdoc />
    public void OnSnapshot(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _latest, snapshot);
        if (IsIdle(snapshot))
        {
            ReleaseIdleWaiters();
        }

        if (Interlocked.Exchange(ref _snapshotQueued, 1) == 0)
        {
            _toSurfaces(DeliverSnapshot);
        }
    }

    /// <summary>
    /// Completes once the engine holds nothing and runs no macro: at once when <see cref="Latest"/> says so, otherwise
    /// with the first such snapshot. It never needs the Surfaces thread, so the synchronous end of the session may
    /// block that thread on it (App/Shutdown): after <c>Terminal(SessionEnd)</c> the engine loop goes on, and this is
    /// how the exit sequence learns that it released everything.
    /// </summary>
    /// <param name="cancellationToken">Abandons the wait.</param>
    public Task WhenNothingHeldAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource waiter;
        lock (_gate)
        {
            // Under the gate, so a snapshot written right after this check releases the waiter added below.
            if (IsIdle(Latest))
            {
                return Task.CompletedTask;
            }

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _idleWaiters.Add(waiter);
        }

        return waiter.Task.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void OnNotice(Message text, NoticeUrgency urgency)
    {
        ArgumentNullException.ThrowIfNull(text);
        _toSurfaces(() => NoticeRaised?.Invoke(this, new EngineNoticeEventArgs(text, urgency)));
    }

    /// <inheritdoc />
    public void OnUsage(ShortcutId shortcut, DateTimeOffset at) =>
        _toSurfaces(() => UsageCounted?.Invoke(this, new UsageCountedEventArgs(shortcut, at)));

    /// <inheritdoc />
    public void OnLastAction(ShortcutId shortcut)
    {
        lock (_gate)
        {
            _lastAction = shortcut;
        }
    }

    private static bool IsIdle(EngineSnapshot snapshot) =>
        snapshot.Held.IsEmpty && snapshot.Macro is null;

    private void ReleaseIdleWaiters()
    {
        lock (_gate)
        {
            foreach (var waiter in _idleWaiters)
            {
                _ = waiter.TrySetResult();
            }

            _idleWaiters.Clear();
        }
    }

    private void DeliverSnapshot()
    {
        // Clear the flag before reading, so a snapshot published while the handlers run queues another delivery.
        Volatile.Write(ref _snapshotQueued, 0);
        SnapshotChanged?.Invoke(this, new EngineSnapshotEventArgs(Latest));
    }
}
