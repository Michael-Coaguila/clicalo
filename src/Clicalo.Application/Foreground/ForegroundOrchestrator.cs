using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Application.Foreground;

/// <summary>
/// The single owner of every foreground change (blueprint §3.6, ADR-0005): typed leases with verified restoration and
/// a rights ladder per origin, and the arbiter that <c>ActivationGuard</c> asks about every surface activation (§3.5).
/// </summary>
/// <remarks>
/// <para>
/// It is an actor: acquisitions, restorations and the reactions to foreground changes, idle timeouts and violations
/// run one at a time behind an asynchronous gate, so there is never more than one active lease. In the app it lives on
/// the SysEvents thread next to <see cref="IForegroundMonitor"/>; nothing here depends on that thread.
/// </para>
/// <para>
/// Thread affinity (blueprint §3.2): the UI thread never calls <c>SetForegroundWindow</c> and the SysEvents thread never
/// blocks. So every entry point leaves the thread that calls it before it touches the foreground or a surface:
/// <see cref="AcquireAsync"/>, the restorations and the reaction to a verified foreground change continue on the thread
/// pool, and <see cref="ReportViolation"/> and the monitor's event return at once.
/// </para>
/// <para>
/// Concurrency rules: a new lease replaces the active one without restoring in between and inherits its previous
/// foreground; a <see cref="LeaseKind.TrayMenu"/> lease replaces any other and nothing replaces it; every lease ends
/// with a terminal event (<see cref="EndActiveLeaseAsync"/>), its idle timeout, or a verified external foreground
/// change (the user switched apps), which ends it without restoring.
/// </para>
/// <para>
/// Never used: <c>AttachThreadInput</c>, a synthetic Alt, <c>LockSetForegroundWindow</c> (§3.6). The only way to the
/// foreground is <see cref="IForegroundControl"/>.
/// </para>
/// </remarks>
public sealed partial class ForegroundOrchestrator
    : IForegroundOrchestrator,
        IActivationArbiter,
        IDisposable
{
    private readonly ForegroundPorts _ports;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _snapshotGate = new();
    private ForegroundSnapshot _current;
    private Leased _leased = Leased.Nothing;
    private ForegroundLease? _active;
    private int _disposed;
    private Task _violationRestore = Task.CompletedTask;
    private Task _foregroundChange = Task.CompletedTask;

    /// <summary>Creates the orchestrator and starts following <see cref="ForegroundPorts.Monitor"/>.</summary>
    /// <param name="ports">The adapters it works through.</param>
    /// <param name="timeProvider">Clock for the retry delay, the rights wait and the idle timeouts.</param>
    /// <param name="logger">Diagnostics without titles or content (LOG-001); none by default.</param>
    public ForegroundOrchestrator(
        ForegroundPorts ports,
        TimeProvider timeProvider,
        ILogger<ForegroundOrchestrator>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _ports = ports;
        _time = timeProvider;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _current = ports.Monitor.Current is { } first
            ? new ForegroundSnapshot(
                first.Window,
                default(ForegroundEpoch).Next(),
                first.ObservedAt
            )
            : ForegroundSnapshot.Empty;
        ports.Monitor.ExternalForegroundChanged += OnExternalForegroundChanged;
    }

    /// <inheritdoc />
    public ForegroundSnapshot Current
    {
        get
        {
            lock (_snapshotGate)
            {
                return _current;
            }
        }
    }

    /// <summary>The active lease, or null. Read for diagnostics; end it through the lease itself.</summary>
    public ForegroundLease? ActiveLease => Volatile.Read(ref _active);

    /// <inheritdoc />
    public async ValueTask<LeaseResult> AcquireAsync(
        LeaseRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (request.Target.IsNone)
        {
            return Deny(request, ForegroundDenialReason.TargetUnavailable);
        }

        await LeaveCallerThread();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AcquireInsideAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public bool IsActivationLeased(WindowToken window) =>
        !window.IsNone && Volatile.Read(ref _leased).Contains(window);

    /// <inheritdoc />
    public void ReportViolation(ActivationViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        if (IsActivationLeased(violation.Window) || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        LogViolation(_logger, violation.Surface, violation.Message, violation.ProbableCause);

        // ActivationGuard calls this inside the surface's window procedure on the UI thread: return at once and give
        // the foreground back from the thread pool. Awaiting the free gate here would complete synchronously and call
        // SetForegroundWindow inside that window procedure, on a thread that never changes the foreground (§3.2, §3.5).
        var reportedIn = Current.Epoch;
        Volatile.Write(
            ref _violationRestore,
            Task.Run(() => RestoreAfterViolationInBackgroundAsync(violation.Window, reportedIn))
        );
    }

    /// <summary>The latest restoration started by <see cref="ReportViolation"/>; tests await it.</summary>
    internal Task ViolationRestore => Volatile.Read(ref _violationRestore);

    /// <summary>The latest reaction to a verified foreground change that ends a lease; tests await it.</summary>
    internal Task ForegroundChangeHandled => Volatile.Read(ref _foregroundChange);

    /// <summary>
    /// Ends the active lease because of a terminal event of the engine (release all, panic, exit), restoring according
    /// to its kind (blueprint §3.6, concurrency rules).
    /// </summary>
    /// <returns>The restoration outcome, or null when no lease was active.</returns>
    public async ValueTask<RestoreOutcome?> EndActiveLeaseAsync(CancellationToken cancellationToken)
    {
        var lease = Volatile.Read(ref _active);
        return lease is null
            ? null
            : await EndAsync(lease, LeaseEndReason.Terminal, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Stops following the monitor and ends the active lease without restoring (the app is closing), putting
    /// <c>WS_EX_NOACTIVATE</c> back on its surface. Nothing can be acquired or restored afterwards.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _ports.Monitor.ExternalForegroundChanged -= OnExternalForegroundChanged;
        if (Volatile.Read(ref _active) is { } lease)
        {
            Finish(lease, LeaseEndReason.Terminal, outcome: null);
        }

        _gate.Dispose();
    }

    /// <summary>Ends <paramref name="lease"/> (serialized) and restores according to its kind.</summary>
    internal async ValueTask<RestoreOutcome> EndAsync(
        ForegroundLease lease,
        LeaseEndReason reason,
        CancellationToken cancellationToken
    )
    {
        if (lease.Outcome is { } known)
        {
            return known;
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await LeaveCallerThread();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(Volatile.Read(ref _active), lease))
            {
                return lease.Outcome ?? lease.RecordLateOutcome(LateOutcome(lease));
            }

            var outcome = RestoreOutcome.Failed;
            try
            {
                outcome = await GiveBackAsync(
                        lease.PreviousForeground,
                        flashOnFailure: lease.Kind == LeaseKind.TryNowTarget,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            finally
            {
                // WS_EX_NOACTIVATE goes back even when the restoration failed or was cancelled (§3.6).
                Finish(lease, reason, outcome);
            }

            return outcome;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ends <paramref name="lease"/> from a timer callback, observing every failure.</summary>
    internal void EndInBackground(ForegroundLease lease, LeaseEndReason reason) =>
        _ = EndInBackgroundAsync(lease, reason);

    private static TimeSpan? IdleTimeoutOf(LeaseRequest request)
    {
        var idle =
            request.IdleTimeout
            ?? (
                request.Kind == LeaseKind.TextInput
                    ? Timings.Foreground.TextInputLeaseIdle
                    : Timeout.InfiniteTimeSpan
            );
        return idle > TimeSpan.Zero && idle != Timeout.InfiniteTimeSpan ? idle : null;
    }

    private static bool NeedsSurfaceActivation(LeaseKind kind) =>
        kind is LeaseKind.TextInput or LeaseKind.KeyboardNavigation;

    /// <summary>
    /// Continues on the thread pool, always, even when nothing before it went asynchronous: the caller may be the UI
    /// thread (which never changes the foreground) or the SysEvents thread (which never blocks), and the gate completes
    /// synchronously when it is free.
    /// </summary>
    private static ConfiguredTaskAwaitable LeaveCallerThread() =>
        Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

    private async ValueTask<LeaseResult> AcquireInsideAsync(
        LeaseRequest request,
        CancellationToken cancellationToken
    )
    {
        var previous = Volatile.Read(ref _active);
        if (previous is { Kind: LeaseKind.TrayMenu })
        {
            return Deny(request, ForegroundDenialReason.TrayMenuActive);
        }

        var start = Current;
        var inFront = _ports.Control.GetForeground();
        SurfaceId? surface =
            NeedsSurfaceActivation(request.Kind)
            && _ports.Surfaces.TryGetSurface(request.Target, out var found)
                ? found
                : null;

        // The target is leased BEFORE the first attempt: the activation messages it receives while it comes to the
        // front must not look like a REG-01 violation to ActivationGuard.
        Publish(previous?.Target ?? WindowToken.None, request.Target);
        if (surface is { } allowed)
        {
            _ports.SurfaceStyle.AllowActivation(allowed);
        }

        Climb climb;
        try
        {
            climb = await ClimbAsync(request, start, cancellationToken).ConfigureAwait(false);
            if (climb.Step is not null && Changed(start, request.Target))
            {
                // The user switched apps while the target came to the front: give the foreground to their choice.
                if (_ports.Control.GetForeground() == request.Target)
                {
                    _ = _ports.Control.TrySetForeground(Current.Window);
                }

                climb = Climb.Denied(ForegroundDenialReason.ForegroundChanged);
            }
        }
        catch
        {
            Undo(previous, surface);
            throw;
        }

        if (climb.Step is not { } step)
        {
            Undo(previous, surface);
            return Deny(request, climb.Reason);
        }

        var chainPrevious = previous?.ChainPrevious ?? start.Window;
        var returnTo =
            request.Kind == LeaseKind.TryNowTarget ? previous?.Target ?? inFront : chainPrevious;
        var lease = new ForegroundLease(
            this,
            request,
            returnTo,
            chainPrevious,
            Current.Epoch,
            step,
            surface,
            IdleTimeoutOf(request)
        );
        if (previous is not null && previous.MarkEnded(LeaseEndReason.Replaced, outcome: null))
        {
            if (previous.Surface is { } replacedSurface && replacedSurface != surface)
            {
                _ports.SurfaceStyle.RestoreNoActivate(replacedSurface);
            }

            LogLeaseEnded(_logger, previous.Kind, LeaseEndReason.Replaced, null);
        }

        Volatile.Write(ref _active, lease);
        Publish(lease.Target, WindowToken.None);
        lease.StartIdleTimer(_time);
        LogLeaseGranted(_logger, request.Kind, request.Origin, step);
        return new LeaseResult.Granted(lease);
    }

    private async ValueTask<Climb> ClimbAsync(
        LeaseRequest request,
        ForegroundSnapshot start,
        CancellationToken cancellationToken
    )
    {
        if (await TakeAsync(request.Target, cancellationToken).ConfigureAwait(false))
        {
            return Climb.Granted(LadderStep.Direct);
        }

        switch (request.Origin)
        {
            case LeaseOrigin.Touch or LeaseOrigin.Tray or LeaseOrigin.GlobalHotkey:
                // Clícalo received the input (or WM_HOTKEY), so the right is there: one retry.
                if (Changed(start, request.Target))
                {
                    return Climb.Denied(ForegroundDenialReason.ForegroundChanged);
                }

                return await TakeAsync(request.Target, cancellationToken).ConfigureAwait(false)
                    ? Climb.Granted(LadderStep.DirectRetry)
                    : Climb.Denied(ForegroundDenialReason.RightsRefused);

            case LeaseOrigin.UiaInvoke:
                return await ClimbWithRightsHotkeyAsync(request, start, cancellationToken)
                    .ConfigureAwait(false);

            default:
                // Internal timers have no input behind them: step 1 only. Returning to the Control Center flashes it
                // instead (PRB-007).
                if (request.Kind == LeaseKind.ControlCenter)
                {
                    _ports.Control.FlashTaskbar(request.Target);
                }

                return Climb.Denied(ForegroundDenialReason.RightsRefused);
        }
    }

    private async ValueTask<Climb> ClimbWithRightsHotkeyAsync(
        LeaseRequest request,
        ForegroundSnapshot start,
        CancellationToken cancellationToken
    )
    {
        var hotkey = _ports.RightsHotkey;
        if (!hotkey.IsRegistered)
        {
            // Another program owns the chord: injecting it would reach the foreground app.
            return Climb.Denied(ForegroundDenialReason.RightsRefused);
        }

        using var armed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Armed BEFORE the chord is sent, so its WM_HOTKEY cannot arrive unobserved.
        var rights = hotkey.WaitForRightsAsync(armed.Token).AsTask();
        bool sent;
        try
        {
            sent = await _ports
                .KeyEffects.SendRightsHotkeyAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await DisarmAsync(armed, rights).ConfigureAwait(false);
            throw;
        }

        if (!sent)
        {
            await DisarmAsync(armed, rights).ConfigureAwait(false);
            return Climb.Denied(ForegroundDenialReason.RightsRefused);
        }

        var arrived = await rights.ConfigureAwait(false);
        if (arrived)
        {
            // The releases of the chord must reach the app that received its presses before the foreground moves.
            _ = await hotkey.WaitForChordReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Changed(start, request.Target))
        {
            return Climb.Denied(ForegroundDenialReason.ForegroundChanged);
        }

        return arrived && await TakeAsync(request.Target, cancellationToken).ConfigureAwait(false)
            ? Climb.Granted(LadderStep.RightsHotkey)
            : Climb.Denied(ForegroundDenialReason.RightsRefused);
    }

    private static async ValueTask DisarmAsync(CancellationTokenSource armed, Task<bool> rights)
    {
        await armed.CancelAsync().ConfigureAwait(false);
        try
        {
            _ = await rights.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Disarmed on purpose.
        }
    }

    /// <summary>
    /// Gives the foreground to <paramref name="window"/>, verified with <c>GetForegroundWindow</c> (see
    /// <see cref="TakeAsync"/>), with one retry: <see cref="RestoreOutcome.Restored"/> when the first attempt worked,
    /// <see cref="RestoreOutcome.RestoredAfterRetry"/> when the retry did. There is no retry when the user switched to
    /// another app while the first attempt was being verified: the foreground is never taken from their choice.
    /// </summary>
    private async ValueTask<RestoreOutcome> GiveBackAsync(
        WindowToken window,
        bool flashOnFailure,
        CancellationToken cancellationToken
    )
    {
        if (window.IsNone)
        {
            return RestoreOutcome.Failed;
        }

        var start = Current;
        if (await TakeAsync(window, cancellationToken).ConfigureAwait(false))
        {
            return RestoreOutcome.Restored;
        }

        if (Changed(start, window))
        {
            return RestoreOutcome.Failed;
        }

        if (await TakeAsync(window, cancellationToken).ConfigureAwait(false))
        {
            return RestoreOutcome.RestoredAfterRetry;
        }

        if (flashOnFailure)
        {
            _ports.Control.FlashTaskbar(window);
            return RestoreOutcome.Flashed;
        }

        return RestoreOutcome.Failed;
    }

    /// <summary>
    /// One verified attempt: <c>SetForegroundWindow</c> and, when <c>GetForegroundWindow</c> does not show
    /// <paramref name="window"/> yet, the same check every <c>Timings.Foreground.RestoreVerifyInterval</c> for up to
    /// <c>Timings.Foreground.RestoreRetryDelay</c>, without calling again. The thread that owns a window activates it
    /// asynchronously when the call comes from another thread (a surface on the UI thread, another app), so the first
    /// check often comes too early (measured in spike S4). The first look that finds the window confirms the attempt.
    /// </summary>
    private async ValueTask<bool> TakeAsync(WindowToken window, CancellationToken cancellationToken)
    {
        var control = _ports.Control;
        if (control.TrySetForeground(window))
        {
            return true;
        }

        var started = _time.GetTimestamp();
        var confirmed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var timer = _time.CreateTimer(
            _ =>
            {
                if (confirmed.Task.IsCompleted)
                {
                    return;
                }

                if (control.GetForeground() == window)
                {
                    _ = confirmed.TrySetResult(true);
                }
                else if (_time.GetElapsedTime(started) >= Timings.Foreground.RestoreRetryDelay)
                {
                    _ = confirmed.TrySetResult(false);
                }
            },
            null,
            Timings.Foreground.RestoreVerifyInterval,
            Timings.Foreground.RestoreVerifyInterval
        );
        await using var cancelled = cancellationToken.Register(() =>
            confirmed.TrySetCanceled(cancellationToken)
        );
        return await confirmed.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// The outcome of a lease that ended without restoring: the foreground is never taken back, only verified.
    /// </summary>
    private RestoreOutcome LateOutcome(ForegroundLease lease)
    {
        if (
            lease.EndReason == LeaseEndReason.ForegroundChanged
            && !lease.PreviousForeground.IsNone
            && _ports.Control.GetForeground() == lease.PreviousForeground
        )
        {
            return RestoreOutcome.Restored;
        }

        if (
            lease.EndReason == LeaseEndReason.ForegroundChanged
            && lease.Kind == LeaseKind.TryNowTarget
            && !lease.PreviousForeground.IsNone
        )
        {
            _ports.Control.FlashTaskbar(lease.PreviousForeground);
            return RestoreOutcome.Flashed;
        }

        return RestoreOutcome.Failed;
    }

    private void Finish(ForegroundLease lease, LeaseEndReason reason, RestoreOutcome? outcome)
    {
        if (!lease.MarkEnded(reason, outcome))
        {
            return;
        }

        if (lease.Surface is { } surface)
        {
            _ports.SurfaceStyle.RestoreNoActivate(surface);
        }

        if (ReferenceEquals(Volatile.Read(ref _active), lease))
        {
            Volatile.Write(ref _active, null);
            Publish(WindowToken.None, WindowToken.None);
        }

        LogLeaseEnded(_logger, lease.Kind, reason, outcome);
    }

    private void Undo(ForegroundLease? previous, SurfaceId? surface)
    {
        if (surface is { } allowed && allowed != previous?.Surface)
        {
            _ports.SurfaceStyle.RestoreNoActivate(allowed);
        }

        Publish(previous?.Target ?? WindowToken.None, WindowToken.None);
    }

    private LeaseResult.Denied Deny(LeaseRequest request, ForegroundDenialReason reason)
    {
        LogLeaseDenied(_logger, request.Kind, request.Origin, reason);
        return new LeaseResult.Denied(reason);
    }

    /// <summary>
    /// True when the user switched to another app since <paramref name="start"/>: a verified external change to a
    /// window other than the one the request started from and other than <paramref name="target"/> itself (a «Try
    /// now» target is an external app). A late report of the app that was already in front is not a switch, even with
    /// a new epoch (the user went away and came back). The window of <see cref="Current"/> only changes with a new
    /// epoch, so comparing the windows is enough.
    /// </summary>
    private bool Changed(ForegroundSnapshot start, WindowToken target)
    {
        var now = Current;
        return now.Window != start.Window && now.Window != target;
    }

    private void Publish(WindowToken active, WindowToken pending) =>
        Volatile.Write(ref _leased, new Leased(active, pending));

    private void OnExternalForegroundChanged(object? sender, ExternalForegroundChangedEventArgs e)
    {
        // Runs on the monitor's thread (SysEvents) and must not block: update the snapshot, then queue the rest.
        ForegroundSnapshot snapshot;
        lock (_snapshotGate)
        {
            snapshot = new ForegroundSnapshot(
                e.Foreground.Window,
                _current.Epoch.Next(),
                e.Foreground.ObservedAt
            );
            _current = snapshot;
        }

        var lease = Volatile.Read(ref _active);
        if (lease is not null && lease.Target != snapshot.Window)
        {
            Volatile.Write(ref _foregroundChange, EndOnForegroundChangeAsync(lease, snapshot));
        }
    }

    private async Task EndOnForegroundChangeAsync(ForegroundLease lease, ForegroundSnapshot change)
    {
        try
        {
            // Queued for real: the free gate would otherwise run GetForegroundWindow and the cross-thread
            // SetWindowLong of RestoreNoActivate inside the monitor's WinEvent callback.
            await LeaveCallerThread();
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (
                    ReferenceEquals(Volatile.Read(ref _active), lease)
                    && change.Epoch.Value > lease.EpochAtAcquire.Value
                    // A report that arrives after the lease took the front again is stale: the user is not elsewhere.
                    && _ports.Control.GetForeground() != lease.Target
                )
                {
                    // The user chose another app: never take the foreground back from it.
                    Finish(lease, LeaseEndReason.ForegroundChanged, outcome: null);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // The orchestrator was disposed meanwhile.
        }
        catch (Exception ex)
        {
            LogBackgroundFault(_logger, ex);
        }
    }

    private async Task EndInBackgroundAsync(ForegroundLease lease, LeaseEndReason reason)
    {
        try
        {
            _ = await EndAsync(lease, reason, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The orchestrator was disposed meanwhile.
        }
        catch (Exception ex)
        {
            LogBackgroundFault(_logger, ex);
        }
    }

    /// <summary>
    /// The restoration asked for by <c>ActivationGuard</c> (blueprint §3.5, ADR-0024): the foreground goes back to the
    /// last verified external window, with one retry and the taskbar flashing when both attempts fail. While a lease is
    /// active it goes back to the lease's target instead, so a surface activated during a text input does not end that
    /// input by sending the user back to the app (which the monitor would then report as an app switch).
    /// </summary>
    /// <remarks>
    /// The only check before restoring keeps a choice of the user: when the monitor verified an external foreground
    /// after <paramref name="reportedIn"/> and no surface is in front any more, the user (or the restore of an earlier
    /// report) already took the foreground out of the process, and nothing is taken from the app in front now.
    /// </remarks>
    private async Task RestoreAfterViolationInBackgroundAsync(
        WindowToken surface,
        ForegroundEpoch reportedIn
    )
    {
        try
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var now = Current;
                if (now.Epoch.Value > reportedIn.Value)
                {
                    var inFront = _ports.Control.GetForeground();
                    if (inFront != surface && !_ports.Surfaces.TryGetSurface(inFront, out _))
                    {
                        LogViolationEndedMeanwhile(_logger, now.Window);
                        return;
                    }
                }

                var expected = Volatile.Read(ref _active)?.Target ?? now.Window;
                if (!expected.IsNone)
                {
                    var outcome = await GiveBackAsync(
                            expected,
                            flashOnFailure: true,
                            CancellationToken.None
                        )
                        .ConfigureAwait(false);
                    LogViolationRestored(_logger, expected, outcome);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // The orchestrator was disposed meanwhile.
        }
        catch (Exception ex)
        {
            LogBackgroundFault(_logger, ex);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Foreground lease {Kind} granted for {Origin} at ladder step {Step}"
    )]
    private static partial void LogLeaseGranted(
        ILogger logger,
        LeaseKind kind,
        LeaseOrigin origin,
        LadderStep step
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Foreground lease {Kind} denied for {Origin}: {Reason}"
    )]
    private static partial void LogLeaseDenied(
        ILogger logger,
        LeaseKind kind,
        LeaseOrigin origin,
        ForegroundDenialReason reason
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Foreground lease {Kind} ended ({Reason}), restore outcome {Outcome}"
    )]
    private static partial void LogLeaseEnded(
        ILogger logger,
        LeaseKind kind,
        LeaseEndReason reason,
        RestoreOutcome? outcome
    );

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "REG-01 violation on surface {Surface} ({Message}, probable cause {Cause})"
    )]
    private static partial void LogViolation(
        ILogger logger,
        SurfaceId surface,
        ActivationMessage message,
        ActivationCause cause
    );

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Foreground after a REG-01 violation given back to {Window}: {Outcome}"
    )]
    private static partial void LogViolationRestored(
        ILogger logger,
        WindowToken window,
        RestoreOutcome outcome
    );

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "A REG-01 violation ended before its restore ran: {Window} was verified in front meanwhile"
    )]
    private static partial void LogViolationEndedMeanwhile(ILogger logger, WindowToken window);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Foreground orchestrator background work failed"
    )]
    private static partial void LogBackgroundFault(ILogger logger, Exception exception);

    /// <summary>The windows <see cref="IsActivationLeased"/> answers true for: the active and the pending target.</summary>
    private sealed record Leased(WindowToken Active, WindowToken Pending)
    {
        public static Leased Nothing { get; } = new(WindowToken.None, WindowToken.None);

        public bool Contains(WindowToken window) => Active == window || Pending == window;
    }

    /// <summary>The result of climbing the rights ladder: the step that worked, or why none did.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Climb(LadderStep? Step, ForegroundDenialReason Reason)
    {
        public static Climb Granted(LadderStep step) =>
            new(step, ForegroundDenialReason.RightsRefused);

        public static Climb Denied(ForegroundDenialReason reason) => new(null, reason);
    }
}
