using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// «Probar ahora en {app}» as a use case (PRB-004, blueprint §3.6): the Control Center hides, the app comes to the
/// front through a <see cref="LeaseKind.TryNowTarget"/> lease, the action is sent after
/// <c>Timings.TryNow.TryNowSendDelay</c> with the app as required foreground, and after
/// <c>Timings.TryNow.TryNowReturnDelay</c> the Control Center comes back through a new
/// <see cref="LeaseKind.ControlCenter"/> lease, which keeps the app that was in front before the Control Center opened
/// as the place to return to when it closes. If Windows refuses the foreground, the Control Center flashes (PRB-007).
/// </summary>
/// <remarks>
/// Hold is held and Toggle latched for <c>Timings.TryNow.TryNowHoldDuration</c>, then released with a second
/// activation. A blocked combination, an incomplete shortcut and an elevated app (when Clícalo is not) are never tried.
/// Used by the Workspace role, one run at a time; continuations resume on the calling thread.
/// </remarks>
public sealed class TryNowRun
{
    private readonly IForegroundOrchestrator _foreground;
    private readonly IEngineInbox _engine;
    private readonly Func<long> _foregroundEpoch;
    private readonly TimeProvider _time;
    private readonly bool _selfElevated;

    /// <summary>Creates the use case.</summary>
    /// <param name="foreground">The single owner of foreground changes.</param>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="foregroundEpoch">The epoch of the verified external foreground.</param>
    /// <param name="time">The clock of the delays.</param>
    /// <param name="selfElevated">Whether Clícalo runs elevated.</param>
    public TryNowRun(
        IForegroundOrchestrator foreground,
        IEngineInbox engine,
        Func<long> foregroundEpoch,
        TimeProvider time,
        bool selfElevated
    )
    {
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(foregroundEpoch);
        ArgumentNullException.ThrowIfNull(time);
        _foreground = foreground;
        _engine = engine;
        _foregroundEpoch = foregroundEpoch;
        _time = time;
        _selfElevated = selfElevated;
    }

    /// <summary>Whether a try is running: its app switches are not the user's (PRB-006).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Runs a try.</summary>
    /// <param name="shortcut">The shortcut, as saved.</param>
    /// <param name="origin">The profile of its list, or null for Always visible.</param>
    /// <param name="injection">The injection mode of that profile (D24).</param>
    /// <param name="target">The app chosen in «Probar en».</param>
    /// <param name="controlCenter">Hides and shows the Control Center window and names it.</param>
    /// <param name="cancellationToken">Cancels the waits (closing the card, PRB-005).</param>
    /// <returns>The outcome, and the new Control Center lease when it came back in front.</returns>
    public async ValueTask<(TryNowOutcome Outcome, ForegroundLease? Lease)> RunAsync(
        Shortcut shortcut,
        ProfileId? origin,
        InjectionMode injection,
        OpenApp target,
        ITryNowWindow controlCenter,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(controlCenter);
        if (ShortcutCompleteness.Evaluate(shortcut) != CompletenessIssue.None)
        {
            return (TryNowOutcome.Incomplete, null);
        }

        if (ComboWarnings.Of(ActionKinds.ChordOf(shortcut.Action)) == ComboWarning.Blocked)
        {
            return (TryNowOutcome.Blocked, null);
        }

        if (target.Elevated && !_selfElevated)
        {
            return (TryNowOutcome.Elevated, null);
        }

        IsRunning = true;
        try
        {
            return await TryAsync(
                    new Attempt(shortcut, origin, injection, target),
                    controlCenter,
                    cancellationToken
                )
                .ConfigureAwait(true);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async ValueTask<(TryNowOutcome, ForegroundLease?)> TryAsync(
        Attempt attempt,
        ITryNowWindow controlCenter,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        controlCenter.Hide();
        var switched = await _foreground
            .AcquireAsync(
                new LeaseRequest(
                    LeaseKind.TryNowTarget,
                    attempt.Target.Window,
                    LeaseOrigin.Touch,
                    null
                ),
                cancellationToken
            )
            .ConfigureAwait(true);
        if (switched is not LeaseResult.Granted)
        {
            var back = await ComeBackAsync(controlCenter, cancellationToken).ConfigureAwait(true);
            return (TryNowOutcome.NotActivated, back);
        }

        await Task.Delay(Timings.TryNow.TryNowSendDelay, _time, cancellationToken)
            .ConfigureAwait(true);
        var sent = Send(attempt);
        if (sent && attempt.Shortcut.Action is HoldAction or ToggleAction)
        {
            await Task.Delay(Timings.TryNow.TryNowHoldDuration, _time, cancellationToken)
                .ConfigureAwait(true);
            sent = Send(attempt);
        }

        var waited = _time.GetElapsedTime(started);
        if (waited < Timings.TryNow.TryNowReturnDelay)
        {
            await Task.Delay(Timings.TryNow.TryNowReturnDelay - waited, _time, cancellationToken)
                .ConfigureAwait(true);
        }

        var lease = await ComeBackAsync(controlCenter, cancellationToken).ConfigureAwait(true);
        return !sent ? (TryNowOutcome.EngineStopped, lease)
            : lease is null ? (TryNowOutcome.AskedFlashed, null)
            : (TryNowOutcome.Asked, lease);
    }

    private bool Send(Attempt attempt) =>
        _engine.Post(
            new EngineEvent.Activation(
                new ActivationRequest(
                    ActivationPhase.Invoke,
                    ActivationOrigin.UiaInvoke,
                    null,
                    null,
                    _time.GetUtcNow()
                ),
                attempt.Shortcut,
                attempt.Origin,
                attempt.Injection,
                LastExternalPointer: null,
                EditMode: false,
                _foregroundEpoch(),
                new ForegroundWindowId(unchecked((ulong)attempt.Target.Window.Handle))
            )
        );

    /// <summary>
    /// The Control Center shows again (without activating) and asks for the foreground through an internal
    /// <see cref="LeaseKind.ControlCenter"/> lease; when Windows refuses, the orchestrator flashes it (PRB-007).
    /// </summary>
    private async ValueTask<ForegroundLease?> ComeBackAsync(
        ITryNowWindow controlCenter,
        CancellationToken cancellationToken
    )
    {
        controlCenter.Show();
        var result = await _foreground
            .AcquireAsync(
                new LeaseRequest(
                    LeaseKind.ControlCenter,
                    controlCenter.Window,
                    LeaseOrigin.Internal,
                    null
                ),
                cancellationToken
            )
            .ConfigureAwait(true);
        return result is LeaseResult.Granted granted ? granted.Lease : null;
    }

    private sealed record Attempt(
        Shortcut Shortcut,
        ProfileId? Origin,
        InjectionMode Injection,
        OpenApp Target
    );
}
