using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// REG-01 at run time (blueprint §3.5, ADR-0005). Fed synchronously by the activation messages of the surfaces
/// themselves (<c>WM_ACTIVATE</c> other than <c>WA_INACTIVE</c>, <c>WM_NCACTIVATE(TRUE)</c>,
/// <c>WM_ACTIVATEAPP(TRUE)</c>) on the UI thread, so it does not depend on WinEvents (which skip the own process)
/// and also catches Clícalo itself being in front (EC-EJE-05).
/// </summary>
/// <remarks>
/// <para>
/// On an activation: if <see cref="IActivationArbiter.IsActivationLeased"/> says a lease targets the window, it is
/// legitimate. Otherwise it is a VIOLATION: <see cref="IActivationArbiter.ReportViolation"/> (the orchestrator
/// restores the last verified external foreground within <c>Timings.Windowing.ViolationRestoreBudget</c>),
/// <c>WS_EX_NOACTIVATE</c> is applied again, <see cref="Violations"/> (metric <see cref="MetricName"/>) increases by
/// one, <see cref="ViolationDetected"/> is raised and, in Debug builds (the desktop tests of <c>cl desk</c> and the
/// CI run in Debug), <c>Debug.Fail</c> stops the run. A test that forces a violation on purpose replaces the
/// <see cref="Trace.Listeners"/> for the length of the test.
/// </para>
/// <para>
/// One activation reaches the thread as several messages (<c>WM_ACTIVATEAPP</c> to every top-level window, then
/// <c>WM_NCACTIVATE</c> and <c>WM_ACTIVATE</c> to the activated one), all sent while the thread retrieves the one
/// activation event of its queue: they count as ONE violation, which stays open until the UI thread is back in its
/// dispatcher after that burst, and ends earlier when the surface is deactivated, the application loses the
/// activation (<c>WM_ACTIVATEAPP(FALSE)</c>) or the foreground is seen outside the process. Spike S1 showed why the end
/// cannot wait for the deactivation messages or for a look at the foreground: when the restore wins the race against
/// the activation, Windows may never deactivate the surface (no <c>WA_INACTIVE</c>, no <c>WM_ACTIVATEAPP(FALSE)</c>,
/// and no <c>WM_ACTIVATEAPP(TRUE)</c> for the next activation either), and the foreground can come back to the app and
/// be taken again between two looks. A violation still open then swallowed the lone <c>WM_NCACTIVATE(TRUE)</c> of the
/// next forced activation, and the panel kept the foreground: neither detected nor reverted.
/// <c>WM_ACTIVATEAPP(TRUE)</c> is sent to every top-level window of the thread, also when a window that is not a
/// surface (the Control Center under its lease) is activated, so it only counts on the surface that owns the
/// foreground.
/// </para>
/// <para>
/// A <c>WM_NCACTIVATE(TRUE)</c> or <c>WM_ACTIVATE</c> for a surface that does not own the foreground
/// (<c>GetForegroundWindow</c>) is judged by who does. A window of this process without a lease (another surface,
/// the <see cref="OwnerAnchor"/>) means the foreground was taken from the app in front: a violation now. A window
/// outside the process may mean two things that the message alone cannot tell apart: a late message of an activation
/// that has already ended (the orchestrator gives the foreground back from the thread pool while the UI thread still
/// delivers the messages of the activation) or a real activation that <c>GetForegroundWindow</c> does not confirm
/// yet (spike S1 saw a lone <c>WM_ACTIVATE</c> of a forced activation arrive that way). The message is kept from WPF
/// and <c>WS_EX_NOACTIVATE</c> is applied again at once, and the judgment is deferred: once the UI thread has
/// delivered what it had queued and, if the foreground is still outside the process, once more after
/// <c>Timings.Windowing.ActivationRecheck</c>. A window of this process in front without a lease then is one
/// violation, reported from there; anything else was a late message and counts nothing. One forced activation is
/// exactly one violation either way.
/// </para>
/// </remarks>
public sealed class ActivationGuard
{
    /// <summary>Name of the violation counter in metrics and logs.</summary>
    public const string MetricName = "reg01.violations";

    private readonly Func<WindowToken> _foregroundWindow;
    private long _violations;
    private NonActivatingWindow? _openViolation;
    private bool _applicationActive;
    private NonActivatingWindow? _pendingSurface;
    private ActivationMessage _pendingMessage;
    private ActivationCause _pendingCause;
    private int _pendingGeneration;
    private ITimer? _recheck;
    private int _violationGeneration;
    private bool _lastSeenOutside;

    // Surfaces whose WM_ACTIVATE (not WA_INACTIVE) was kept from WPF: their WA_INACTIVE is kept too, so WPF never sees
    // the end of an activation it did not see begin. Only touched on the UI thread.
    private readonly HashSet<NonActivatingWindow> _keptActivations = [];

    /// <summary>Creates the guard that reports to <paramref name="arbiter"/> and stamps with <paramref name="timeProvider"/>.</summary>
    public ActivationGuard(IActivationArbiter arbiter, TimeProvider timeProvider)
        : this(
            arbiter,
            timeProvider,
            static () => new WindowToken((nint)PInvoke.GetForegroundWindow())
        ) { }

    /// <summary>
    /// Creates the guard with the source of <c>GetForegroundWindow</c>: the headless tests, whose surfaces are never
    /// shown, say which window owns the foreground.
    /// </summary>
    public ActivationGuard(
        IActivationArbiter arbiter,
        TimeProvider timeProvider,
        Func<WindowToken> foregroundWindow
    )
    {
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(foregroundWindow);
        Arbiter = arbiter;
        Clock = timeProvider;
        _foregroundWindow = foregroundWindow;
    }

    /// <summary>Violations detected since the process started (<see cref="MetricName"/>).</summary>
    public long Violations => Interlocked.Read(ref _violations);

    /// <summary>Raised on the UI thread after each violation has been reported and repaired.</summary>
    public event EventHandler<ActivationViolationEventArgs>? ViolationDetected;

    /// <summary>The orchestrator side that decides whether an activation is leased.</summary>
    public IActivationArbiter Arbiter { get; }

    /// <summary>Clock for <see cref="ActivationViolation.DetectedAt"/> and for the deferred judgment.</summary>
    public TimeProvider Clock { get; }

    /// <summary>
    /// Called by the common hook of <paramref name="surface"/> for each activation message. Returns true when the
    /// message is kept from WPF: the first message of a violation, which has been reported, repaired and counted; the
    /// later messages of the same activation, which do not count again; and a message for a surface that does not own
    /// the foreground while the foreground is outside the process, whose judgment is deferred.
    /// </summary>
    /// <param name="surface">The surface that received the message.</param>
    /// <param name="message">Which activation message.</param>
    /// <param name="probableCause">What the hook saw just before (DPI change, topmost, show).</param>
    public bool OnActivated(
        NonActivatingWindow surface,
        ActivationMessage message,
        ActivationCause probableCause
    )
    {
        ArgumentNullException.ThrowIfNull(surface);
        var window = surface.SurfaceWindow;
        if (window.IsNone)
        {
            return false;
        }

        if (message == ActivationMessage.ActivateApp && !_applicationActive)
        {
            // WM_ACTIVATEAPP(TRUE) reaches this thread only when the application was inactive, first of all the
            // messages of an activation. A violation still open here belongs to an earlier activation whose end never
            // arrived (a lost deactivation, a late message after the restore): it is over, and this activation must
            // be judged on its own instead of being swallowed as part of it. So is a deferred judgment: the messages
            // of this activation decide.
            _applicationActive = true;
            CloseViolation();
            ClearPending();
        }

        if (Arbiter.IsActivationLeased(window))
        {
            return false;
        }

        var foreground = _foregroundWindow();
        var ownsForeground = foreground == window;
        var outside = !ownsForeground && !IsTakenByThisProcess(foreground);
        if (outside)
        {
            // The foreground is outside the process: whatever was open has ended.
            CloseViolation();
            _lastSeenOutside = true;
        }

        if (message == ActivationMessage.ActivateApp && !ownsForeground)
        {
            // Another window of this thread is being activated: its own messages decide.
            return false;
        }

        if (_openViolation is null)
        {
            if (outside)
            {
                // The foreground is outside the process: a late message, or an activation not confirmed yet. Keep it
                // from WPF, repair, and judge it once the thread has caught up.
                surface.ReapplyNonActivation();
                Defer(surface, message, probableCause);
            }
            else
            {
                Report(surface, message, probableCause);
            }
        }

        if (message == ActivationMessage.Activate)
        {
            _ = _keptActivations.Add(surface);
        }

        return true;
    }

    /// <summary>
    /// Called by the common hook for <c>WM_ACTIVATE(WA_INACTIVE)</c>, <c>WM_NCACTIVATE(FALSE)</c> and
    /// <c>WM_ACTIVATEAPP(FALSE)</c>: closes the open violation when its surface is deactivated, the whole application
    /// loses the activation, or the foreground is already outside the process. Returns true for the
    /// <c>WA_INACTIVE</c> that ends an activation the hook kept from WPF, so the hook keeps that one too.
    /// </summary>
    internal bool OnDeactivated(NonActivatingWindow surface, ActivationMessage message)
    {
        if (message == ActivationMessage.ActivateApp)
        {
            _applicationActive = false;
        }

        var kept = message == ActivationMessage.Activate && _keptActivations.Remove(surface);
        var outside = !IsTakenByThisProcess(_foregroundWindow());
        _lastSeenOutside = outside;
        var open = _openViolation;
        if (open is null)
        {
            return kept;
        }

        var sameSurface = ReferenceEquals(open, surface);
        if (
            message == ActivationMessage.ActivateApp
            || (sameSurface && message == ActivationMessage.Activate)
            || outside
        )
        {
            CloseViolation();
        }

        return kept;
    }

    /// <summary>A destroyed surface can no longer be deactivated: its open violation ends with it.</summary>
    internal void Forget(NonActivatingWindow surface)
    {
        if (ReferenceEquals(_openViolation, surface))
        {
            CloseViolation();
        }

        if (ReferenceEquals(_pendingSurface, surface))
        {
            ClearPending();
        }

        _ = _keptActivations.Remove(surface);
    }

    private static bool IsOfThisProcess(WindowToken window)
    {
        _ = PInvoke.GetWindowThreadProcessId((HWND)window.Handle, out var processId);
        return processId != 0 && processId == (uint)Environment.ProcessId;
    }

    /// <summary>
    /// True when <paramref name="foreground"/> is a window of this process that no lease lets activate: the
    /// foreground was taken from the app in front.
    /// </summary>
    private bool IsTakenByThisProcess(WindowToken foreground) =>
        !foreground.IsNone
        && IsOfThisProcess(foreground)
        && !Arbiter.IsActivationLeased(foreground);

    /// <summary>Opens, reports, repairs and counts one violation of <paramref name="surface"/>.</summary>
    private void Report(
        NonActivatingWindow surface,
        ActivationMessage message,
        ActivationCause probableCause
    )
    {
        ClearPending();
        _openViolation = surface;
        EndWithTheBurst(surface);

        // Nothing the surfaces did explains it, and the last look at the foreground found it in another application:
        // the activation came from outside (a lone WM_NCACTIVATE, without the WM_ACTIVATEAPP that notes it, in S1).
        var cause =
            probableCause == ActivationCause.Unknown && _lastSeenOutside
                ? ActivationCause.External
                : probableCause;

        // The restore gives the foreground back to the app this activation took it from, so until a deactivation looks
        // at the foreground again, the next activation that nothing explains is taken as coming from outside as well:
        // Windows may deliver no deactivation at all to the surface, and then the next forced activation is a lone
        // WM_NCACTIVATE(TRUE), without the WM_ACTIVATEAPP(TRUE) that would say so (spike S1).
        _lastSeenOutside = cause == ActivationCause.External;
        var violation = new ActivationViolation(
            surface.Id,
            surface.SurfaceWindow,
            message,
            cause,
            Clock.GetUtcNow()
        );
        try
        {
            Arbiter.ReportViolation(violation);
        }
        finally
        {
            surface.ReapplyNonActivation();
            Record(violation);
        }

        Debug.Fail(
            string.Create(
                CultureInfo.InvariantCulture,
                $"REG-01 violation: surface {violation.Surface} was activated without a lease ({violation.Message}, probable cause {violation.ProbableCause})."
            )
        );
    }

    /// <summary>
    /// Keeps the first message of an unconfirmed activation and queues its judgment behind what the UI thread has
    /// already queued. Later messages of the same activation join the pending one.
    /// </summary>
    private void Defer(
        NonActivatingWindow surface,
        ActivationMessage message,
        ActivationCause probableCause
    )
    {
        if (_pendingSurface is not null)
        {
            return;
        }

        _pendingSurface = surface;
        _pendingMessage = message;
        // The foreground was in another application when the message arrived: unless the surfaces were doing
        // something that explains it, the activation came from outside.
        _pendingCause =
            probableCause == ActivationCause.Unknown ? ActivationCause.External : probableCause;
        var generation = ++_pendingGeneration;
        _ = surface.Dispatcher.InvokeAsync(
            () => Judge(generation, final: false),
            DispatcherPriority.Background
        );
    }

    /// <summary>
    /// The deferred judgment: a window of this process in front without a lease is one violation; otherwise, the first
    /// time, one more look after <c>Timings.Windowing.ActivationRecheck</c>, and the second time it was a late message.
    /// </summary>
    private void Judge(int generation, bool final)
    {
        var surface = _pendingSurface;
        if (surface is null || generation != _pendingGeneration)
        {
            return;
        }

        if (_openViolation is not null || surface.SurfaceWindow.IsNone)
        {
            // Counted meanwhile by the messages of the same activation, or the surface is gone.
            ClearPending();
            return;
        }

        if (IsTakenByThisProcess(_foregroundWindow()))
        {
            Report(surface, _pendingMessage, _pendingCause);
            return;
        }

        _lastSeenOutside = true;
        if (final)
        {
            ClearPending();
            return;
        }

        var dispatcher = surface.Dispatcher;
        _recheck?.Dispose();
        _recheck = Clock.CreateTimer(
            _ => _ = dispatcher.InvokeAsync(() => Judge(generation, final: true)),
            null,
            Timings.Windowing.ActivationRecheck,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>
    /// Ends the violation just opened once the UI thread is back in its dispatcher: every message of the activation that
    /// opened it was sent during the same retrieval, so whatever activates a surface after that is another activation,
    /// even when no deactivation message and no look at the foreground came in between (spike S1).
    /// </summary>
    private void EndWithTheBurst(NonActivatingWindow surface)
    {
        var opened = ++_violationGeneration;
        _ = surface.Dispatcher.InvokeAsync(
            () =>
            {
                if (opened == _violationGeneration)
                {
                    CloseViolation();
                }
            },
            DispatcherPriority.Send
        );
    }

    private void CloseViolation()
    {
        _openViolation = null;
        _violationGeneration++;
    }

    private void ClearPending()
    {
        _pendingSurface = null;
        _pendingGeneration++;
        _recheck?.Dispose();
        _recheck = null;
    }

    /// <summary>Counts <paramref name="violation"/> and raises <see cref="ViolationDetected"/>.</summary>
    private void Record(ActivationViolation violation)
    {
        Interlocked.Increment(ref _violations);
        ViolationDetected?.Invoke(this, new ActivationViolationEventArgs(violation));
    }
}
