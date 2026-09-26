using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>
/// A granted foreground lease (blueprint §3.6). Only <c>ForegroundOrchestrator</c> creates it. Ending it always
/// gives the foreground back according to the policy of its <see cref="Kind"/> and puts <c>WS_EX_NOACTIVATE</c>
/// back on a surface target. A newer lease replaces it without restoring in between (it inherits
/// <see cref="PreviousForeground"/>); a terminal engine event or a change of epoch ends it.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class ForegroundLease : IAsyncDisposable
{
    internal ForegroundLease(
        LeaseKind kind,
        WindowToken target,
        WindowToken previousForeground,
        ForegroundEpoch epochAtAcquire
    )
    {
        Kind = kind;
        Target = target;
        PreviousForeground = previousForeground;
        EpochAtAcquire = epochAtAcquire;
    }

    /// <summary>The kind of lease.</summary>
    public LeaseKind Kind { get; }

    /// <summary>The window the lease brought to the foreground.</summary>
    public WindowToken Target { get; }

    /// <summary>The external foreground to return to.</summary>
    public WindowToken PreviousForeground { get; }

    /// <summary>The foreground epoch when the lease was granted.</summary>
    public ForegroundEpoch EpochAtAcquire { get; }

    /// <summary>
    /// Ends the lease now and gives the foreground back, verified with <c>GetForegroundWindow</c> and one retry
    /// after <c>Timings.Foreground.RestoreRetryDelay</c>. Idempotent: later calls return the first outcome.
    /// </summary>
    public ValueTask<RestoreOutcome> RestoreAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("M1 foreground package: lease restoration.");

    /// <summary>
    /// Ends the lease if <see cref="RestoreAsync"/> was not called; always restores according to the kind and puts
    /// <c>WS_EX_NOACTIVATE</c> back.
    /// </summary>
    public ValueTask DisposeAsync() =>
        throw new NotImplementedException("M1 foreground package: lease disposal.");
}
