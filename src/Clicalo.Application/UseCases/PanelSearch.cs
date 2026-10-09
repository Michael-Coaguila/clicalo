using Clicalo.Application.Coordinators;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Application.UseCases;

/// <summary>
/// The only place where the panel takes the keyboard (REG-01's single exception, BUS-002, blueprint §3.6 «Ciclo de
/// <c>TextInput</c>»): the search field holds a <see cref="LeaseKind.TextInput"/> lease only while the user types or
/// dictates, and a result runs only after the app the user was in is back in front.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><see cref="TakeKeyboardAsync"/>: the orchestrator remembers the app in front, lifts <c>WS_EX_NOACTIVATE</c>
/// from the panel and brings it forward (the rights ladder of the origin); the view then focuses the field.</item>
/// <item>Typing calls <see cref="KeepAlive"/>; the lease ends by itself after
/// <c>Timings.Foreground.TextInputLeaseIdle</c> or when the user switches apps (<see cref="KeyboardEnded"/>).</item>
/// <item><see cref="RunTappedAsync"/> and <see cref="RunInvokedAsync"/> give the foreground back first, verified, and
/// only then post the activation, with the restored window as <see cref="EngineEvent.Activation.RequiredForeground"/>.
/// If the app does not come back, nothing is sent (BUS-002 d).</item>
/// </list>
/// Used by the Surfaces role only, one call at a time; continuations resume on the calling thread. The orchestrator
/// itself leaves that thread before it touches the foreground (§3.2).
/// </remarks>
public sealed class PanelSearch
{
    private readonly IForegroundOrchestrator _foreground;
    private readonly IEngineInbox _engine;
    private readonly Func<long> _foregroundEpoch;
    private readonly TimeProvider _time;
    private readonly ITouchKeyboard? _keyboard;
    private ForegroundLease? _lease;
    private bool _keyboardShown;

    /// <summary>Creates the search cycle.</summary>
    /// <param name="foreground">The single owner of foreground changes.</param>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="foregroundEpoch">
    /// The epoch of the verified external foreground (<see cref="ForegroundChangeCoordinator.CurrentEpoch"/>).
    /// </param>
    /// <param name="time">The clock of the waits.</param>
    /// <param name="keyboard">The touch keyboard and dictation; null where there is none.</param>
    public PanelSearch(
        IForegroundOrchestrator foreground,
        IEngineInbox engine,
        Func<long> foregroundEpoch,
        TimeProvider time,
        ITouchKeyboard? keyboard
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
        _keyboard = keyboard;
    }

    /// <summary>Whether the field holds the keyboard right now.</summary>
    public bool HasKeyboard => _lease is { IsActive: true };

    /// <summary>
    /// Completes when the current lease ends, with the reason (null without a lease): the panel closes the search when
    /// the user switched apps (<see cref="LeaseEndReason.ForegroundChanged"/>, BUS-001).
    /// </summary>
    public Task<LeaseEndReason>? KeyboardEnded => _lease?.Ended;

    /// <summary>
    /// Lets the search field take the keyboard (BUS-002 a and b): a <see cref="LeaseKind.TextInput"/> lease on the
    /// panel. Already holding it, it only postpones the idle timeout.
    /// </summary>
    /// <param name="panel">The panel window, which the lease brings forward.</param>
    /// <param name="origin">What opened the search; it selects the rights ladder.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the panel may focus the field now; false when Windows refused (the panel says so).</returns>
    public async ValueTask<bool> TakeKeyboardAsync(
        WindowToken panel,
        LeaseOrigin origin,
        CancellationToken cancellationToken
    )
    {
        if (HasKeyboard)
        {
            KeepAlive();
            return true;
        }

        var result = await _foreground
            .AcquireAsync(
                new LeaseRequest(LeaseKind.TextInput, panel, origin, IdleTimeout: null),
                cancellationToken
            )
            .ConfigureAwait(true);
        if (result is not LeaseResult.Granted granted)
        {
            return false;
        }

        _lease = granted.Lease;
        return true;
    }

    /// <summary>The user typed or dictated into the field: the lease lives on (its idle timeout starts again).</summary>
    public void KeepAlive() => _lease?.KeepAlive();

    /// <summary>
    /// The field was tapped: the touch keyboard comes up for it (ACC-007: only this field opens it). Only while the field
    /// holds the keyboard.
    /// </summary>
    /// <param name="panel">The panel window, where the focused field is.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when Windows accepted to show the touch keyboard.</returns>
    public async ValueTask<bool> ShowTouchKeyboardAsync(
        WindowToken panel,
        CancellationToken cancellationToken
    )
    {
        if (!HasKeyboard || _keyboard is null)
        {
            return false;
        }

        _keyboardShown = await _keyboard
            .ShowKeyboardAsync(panel, cancellationToken)
            .ConfigureAwait(true);
        return _keyboardShown;
    }

    /// <summary>
    /// 🎤 «Dictar» (BUS-003): Windows dictation (Win+H) for the focused field. Only while the field holds the keyboard,
    /// so the dictated text never lands in another app.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the dictation chord was sent.</returns>
    public async ValueTask<bool> DictateAsync(CancellationToken cancellationToken)
    {
        if (!HasKeyboard || _keyboard is null)
        {
            return false;
        }

        KeepAlive();
        return await _keyboard.StartDictationAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The search closes or the field is left (Esc, PAN-010): the foreground goes back to the app, verified, and the
    /// touch keyboard Clícalo showed goes away. Nothing is sent.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How the foreground was given back; null when the field did not hold the keyboard.</returns>
    public async ValueTask<RestoreOutcome?> ReleaseKeyboardAsync(
        CancellationToken cancellationToken
    )
    {
        var lease = _lease;
        _lease = null;
        RestoreOutcome? outcome = null;
        if (lease is not null)
        {
            outcome = await lease.RestoreAsync(cancellationToken).ConfigureAwait(true);
        }

        await HideTouchKeyboardAsync(cancellationToken).ConfigureAwait(true);
        return outcome;
    }

    /// <summary>
    /// An accepted tap lifted on a result (BUS-005): it runs with its origin profile, after the foreground is back on the
    /// app. A Hold result is latched as when invoked (EJE-005): its contact may lift while the foreground is still
    /// being given back, and a hold that started after its contact ended would stay down.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="contactId">The pointer id of the contact.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="summary">Duration, displacement and palm of the contact (the touch filter applies, TAC-002).</param>
    /// <param name="at">When the contact lifted.</param>
    /// <param name="cancellationToken">Cancels the wait for the foreground.</param>
    public ValueTask<SearchRunOutcome> RunTappedAsync(
        TileBinding result,
        uint contactId,
        PointerKind device,
        ContactSummary summary,
        DateTimeOffset at,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        var origin = OriginOf(device);
        var request =
            result.Shortcut.Action is HoldAction
                ? new ActivationRequest(ActivationPhase.Invoke, origin, null, null, at)
                : new ActivationRequest(
                    ActivationPhase.ContactEnded,
                    origin,
                    unchecked((int)contactId),
                    summary,
                    at
                );
        return RunAsync(result, request, cancellationToken);
    }

    /// <summary>
    /// A UI Automation Invoke or Toggle of a result (voice, Narrator, switches; EJE-005): it runs as an invocation after
    /// the foreground is back on the app.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">Cancels the wait for the foreground.</param>
    public ValueTask<SearchRunOutcome> RunInvokedAsync(
        TileBinding result,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        return RunAsync(
            result,
            new ActivationRequest(
                ActivationPhase.Invoke,
                ActivationOrigin.UiaInvoke,
                null,
                null,
                _time.GetUtcNow()
            ),
            cancellationToken
        );
    }

    private static ActivationOrigin OriginOf(PointerKind device) =>
        device switch
        {
            PointerKind.Finger => ActivationOrigin.Touch,
            PointerKind.Pen => ActivationOrigin.Pen,
            PointerKind.Mouse => ActivationOrigin.Mouse,
            _ => throw new ArgumentOutOfRangeException(nameof(device), device, null),
        };

    private static ForegroundWindowId WindowIdOf(WindowToken window) =>
        new(unchecked((ulong)window.Handle));

    private async ValueTask<SearchRunOutcome> RunAsync(
        TileBinding result,
        ActivationRequest request,
        CancellationToken cancellationToken
    )
    {
        ForegroundWindowId? required = null;
        var lease = _lease;
        _lease = null;
        if (lease is not null)
        {
            // BUS-002 c: the app comes back first, verified with one retry; d: if it does not, nothing is sent.
            var wasActive = lease.IsActive;
            var epoch = _foregroundEpoch();
            var restored = await lease.RestoreAsync(cancellationToken).ConfigureAwait(true);
            await HideTouchKeyboardAsync(cancellationToken).ConfigureAwait(true);
            if (restored is not (RestoreOutcome.Restored or RestoreOutcome.RestoredAfterRetry))
            {
                return SearchRunOutcome.NotSent;
            }

            required = WindowIdOf(lease.PreviousForeground);
            if (wasActive)
            {
                await WaitForReturnEpochAsync(epoch, cancellationToken).ConfigureAwait(true);
            }
        }

        var posted = _engine.Post(
            new EngineEvent.Activation(
                request,
                result.Shortcut,
                result.OriginProfile,
                result.Injection,
                LastExternalPointer: null,
                EditMode: false,
                _foregroundEpoch(),
                required
            )
        );
        return posted ? SearchRunOutcome.Sent : SearchRunOutcome.EngineStopped;
    }

    /// <summary>
    /// The monitor reports the app's return as a new foreground epoch (same app, not a user switch), and the engine
    /// refuses presses stamped with an older one (INV-6). Waits, at most <c>RestoreRetryDelay</c>, until the epoch moves
    /// on, so the activation carries the epoch the engine will hold; if it never moves, the current one goes.
    /// </summary>
    private async ValueTask WaitForReturnEpochAsync(
        long before,
        CancellationToken cancellationToken
    )
    {
        var deadline = _time.GetUtcNow() + Timings.Foreground.RestoreRetryDelay;
        while (_foregroundEpoch() == before && _time.GetUtcNow() < deadline)
        {
            await Task.Delay(Timings.Foreground.RestoreVerifyInterval, _time, cancellationToken)
                .ConfigureAwait(true);
        }
    }

    private async ValueTask HideTouchKeyboardAsync(CancellationToken cancellationToken)
    {
        if (!_keyboardShown || _keyboard is null)
        {
            return;
        }

        _keyboardShown = false;
        await _keyboard.HideKeyboardAsync(cancellationToken).ConfigureAwait(true);
    }
}
