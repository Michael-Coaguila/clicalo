using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>
/// A granted foreground lease (blueprint §3.6). Only <see cref="ForegroundOrchestrator"/> creates it. Ending it always
/// gives the foreground back according to the policy of its <see cref="Kind"/> and puts <c>WS_EX_NOACTIVATE</c>
/// back on a surface target. A newer lease replaces it without restoring in between (it inherits
/// <see cref="PreviousForeground"/>); a terminal engine event or a change of epoch ends it.
/// </summary>
/// <remarks>
/// Thread-safe: its state changes only inside the orchestrator's serialized work and is read through volatile fields.
/// </remarks>
public sealed class ForegroundLease : IAsyncDisposable
{
    private readonly ForegroundOrchestrator _owner;
    private readonly TaskCompletionSource<LeaseEndReason> _ended = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private int _endReason = -1;
    private int _outcome = -1;
    private ITimer? _idleTimer;

    internal ForegroundLease(
        ForegroundOrchestrator owner,
        LeaseRequest request,
        WindowToken previousForeground,
        WindowToken chainPrevious,
        ForegroundEpoch epochAtAcquire,
        LadderStep grantedAt,
        SurfaceId? surface,
        TimeSpan? idleTimeout
    )
    {
        _owner = owner;
        Kind = request.Kind;
        Origin = request.Origin;
        Target = request.Target;
        PreviousForeground = previousForeground;
        ChainPrevious = chainPrevious;
        EpochAtAcquire = epochAtAcquire;
        GrantedAt = grantedAt;
        Surface = surface;
        IdleTimeout = idleTimeout;
    }

    /// <summary>The kind of lease.</summary>
    public LeaseKind Kind { get; }

    /// <summary>What triggered the request; it selected the rights ladder.</summary>
    public LeaseOrigin Origin { get; }

    /// <summary>The window the lease brought to the foreground.</summary>
    public WindowToken Target { get; }

    /// <summary>
    /// Where <see cref="RestoreAsync"/> returns: the external foreground from before the first lease of the chain
    /// (inherited by replacements); for <see cref="LeaseKind.TryNowTarget"/>, the Control Center that was in front.
    /// <see cref="WindowToken.None"/> when there was none, and then restoring reports <see cref="RestoreOutcome.Failed"/>.
    /// </summary>
    public WindowToken PreviousForeground { get; }

    /// <summary>The foreground epoch when the lease was granted.</summary>
    public ForegroundEpoch EpochAtAcquire { get; }

    /// <summary>The step of the rights ladder that brought <see cref="Target"/> to the front.</summary>
    public LadderStep GrantedAt { get; }

    /// <summary>True until the lease ends, for whatever reason.</summary>
    public bool IsActive => Volatile.Read(ref _endReason) < 0;

    /// <summary>Why the lease ended; null while it is active.</summary>
    public LeaseEndReason? EndReason =>
        Volatile.Read(ref _endReason) is var reason and >= 0 ? (LeaseEndReason)reason : null;

    /// <summary>
    /// Completes when the lease ends, with the reason (for example, the search closes when the user switched apps).
    /// Continuations run asynchronously, never inside the orchestrator.
    /// </summary>
    public Task<LeaseEndReason> Ended => _ended.Task;

    /// <summary>
    /// The external foreground the chain of leases started from; replacements inherit it (for
    /// <see cref="LeaseKind.TryNowTarget"/> it differs from <see cref="PreviousForeground"/>).
    /// </summary>
    internal WindowToken ChainPrevious { get; }

    /// <summary>The surface whose activation the lease allowed; null when the target is not a surface.</summary>
    internal SurfaceId? Surface { get; }

    /// <summary>The idle timeout in force; null for none.</summary>
    internal TimeSpan? IdleTimeout { get; }

    /// <summary>The outcome of the first restoration, once known.</summary>
    internal RestoreOutcome? Outcome =>
        Volatile.Read(ref _outcome) is var outcome and >= 0 ? (RestoreOutcome)outcome : null;

    /// <summary>
    /// Ends the lease now and gives the foreground back, verified with <c>GetForegroundWindow</c> and one retry
    /// after <c>Timings.Foreground.RestoreRetryDelay</c>. Idempotent: later calls return the first outcome. When the
    /// lease had already ended without restoring (<see cref="LeaseEndReason.Replaced"/> or
    /// <see cref="LeaseEndReason.ForegroundChanged"/>), it never takes the foreground back: it reports
    /// <see cref="RestoreOutcome.Restored"/> only if <see cref="PreviousForeground"/> is in front anyway.
    /// </summary>
    public ValueTask<RestoreOutcome> RestoreAsync(CancellationToken cancellationToken) =>
        _owner.EndAsync(this, LeaseEndReason.Released, cancellationToken);

    /// <summary>
    /// Postpones the idle timeout (<see cref="LeaseRequest.IdleTimeout"/>): call it on every interaction with the
    /// target, such as a key typed into the search. No effect once the lease has ended or without a timeout. May be
    /// called from any thread, also while the lease is ending.
    /// </summary>
    public void KeepAlive()
    {
        if (!IsActive || IdleTimeout is not { } idle)
        {
            return;
        }

        try
        {
            _ = Volatile.Read(ref _idleTimer)?.Change(idle, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // The lease ended on another thread between the check and the call: nothing to postpone.
        }
    }

    /// <summary>
    /// Ends the lease if <see cref="RestoreAsync"/> was not called; always restores according to the kind and puts
    /// <c>WS_EX_NOACTIVATE</c> back.
    /// </summary>
    public async ValueTask DisposeAsync() =>
        _ = await RestoreAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>Starts the idle timer; called once, when the lease is granted.</summary>
    internal void StartIdleTimer(TimeProvider timeProvider)
    {
        if (IdleTimeout is { } idle)
        {
            Volatile.Write(
                ref _idleTimer,
                timeProvider.CreateTimer(
                    static state => ((ForegroundLease)state!).OnIdle(),
                    this,
                    idle,
                    Timeout.InfiniteTimeSpan
                )
            );
        }
    }

    /// <summary>
    /// Marks the lease as ended; the first call wins. Stops the idle timer and completes <see cref="Ended"/>.
    /// </summary>
    internal bool MarkEnded(LeaseEndReason reason, RestoreOutcome? outcome)
    {
        if (Interlocked.CompareExchange(ref _endReason, (int)reason, -1) != -1)
        {
            return false;
        }

        if (outcome is { } value)
        {
            Volatile.Write(ref _outcome, (int)value);
        }

        Interlocked.Exchange(ref _idleTimer, null)?.Dispose();
        _ended.TrySetResult(reason);
        return true;
    }

    /// <summary>Records the outcome computed after the lease had ended without restoring; the first one stays.</summary>
    internal RestoreOutcome RecordLateOutcome(RestoreOutcome outcome)
    {
        var first = Interlocked.CompareExchange(ref _outcome, (int)outcome, -1);
        return first < 0 ? outcome : (RestoreOutcome)first;
    }

    private void OnIdle() => _owner.EndInBackground(this, LeaseEndReason.IdleTimeout);
}
