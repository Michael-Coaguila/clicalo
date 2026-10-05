using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using Clicalo.Application.Ports;
using Windows.Win32;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// REG-01 at run time (blueprint §3.5, ADR-0005, ADR-0024). Fed synchronously by the activation messages of the
/// surfaces themselves (<c>WM_ACTIVATE</c> other than <c>WA_INACTIVE</c>, <c>WM_NCACTIVATE(TRUE)</c>,
/// <c>WM_ACTIVATEAPP(TRUE)</c>) on the UI thread, so it does not depend on WinEvents (which skip the own process) and
/// also catches Clícalo itself being in front (EC-EJE-05).
/// </summary>
/// <remarks>
/// <para>
/// One simple rule. An activation of a surface that no lease targets
/// (<see cref="IActivationArbiter.IsActivationLeased"/>) is a violation: <c>WS_EX_NOACTIVATE</c> is applied again at
/// once, <see cref="Violations"/> (metric <see cref="MetricName"/>) increases by one, <see cref="ViolationDetected"/> is
/// raised, and ONE restoration is asked of the arbiter (<see cref="IActivationArbiter.ReportViolation"/>, which gives
/// the foreground back to the last verified external app from the thread pool). The request leaves the window
/// procedure: it is queued on the dispatcher, and every activation that arrives while it is still queued joins it
/// instead of counting again (the several messages of one activation, or a second forced activation before the UI
/// thread is back in its dispatcher). The restoration therefore always runs after every activation it covers. In Debug
/// builds <c>Debug.Fail</c> stops the run; a test that forces a violation on purpose replaces the
/// <see cref="Trace.Listeners"/> for the length of the test.
/// </para>
/// <para>
/// <c>WM_ACTIVATEAPP(TRUE)</c> reaches every top-level window of the thread, also when a window that is not a surface
/// (the Control Center under its lease) is the one activated, so it only counts on the surface that owns the
/// foreground. A violating <c>WM_ACTIVATE</c> is kept from WPF and <c>DefWindowProc</c> (no focus moves in), and so is
/// its matching <c>WA_INACTIVE</c>.
/// </para>
/// </remarks>
public sealed class ActivationGuard
{
    /// <summary>Name of the violation counter in metrics and logs.</summary>
    public const string MetricName = "reg01.violations";

    private readonly Func<WindowToken> _foregroundWindow;
    private long _violations;

    // The restoration asked for and not yet handed to the arbiter. Only touched on the UI thread.
    private ActivationViolation? _pending;

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

    /// <summary>Raised on the UI thread after each violation has been counted and repaired.</summary>
    public event EventHandler<ActivationViolationEventArgs>? ViolationDetected;

    /// <summary>The orchestrator side that decides whether an activation is leased.</summary>
    public IActivationArbiter Arbiter { get; }

    /// <summary>Clock for <see cref="ActivationViolation.DetectedAt"/>.</summary>
    public TimeProvider Clock { get; }

    /// <summary>
    /// Called by the common hook of <paramref name="surface"/> for each activation message. Returns true when the
    /// message is a violation and must be kept from WPF.
    /// </summary>
    /// <param name="surface">The surface that received the message.</param>
    /// <param name="message">Which activation message.</param>
    /// <param name="probableCause">What the hook saw just before (DPI change, topmost, show, another app).</param>
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

        if (Arbiter.IsActivationLeased(window))
        {
            if (message == ActivationMessage.Activate)
            {
                // WPF sees this activation begin, so it must see its end.
                _ = _keptActivations.Remove(surface);
            }

            return false;
        }

        if (message == ActivationMessage.ActivateApp && _foregroundWindow() != window)
        {
            // Another window of this thread is the one being activated: its own messages decide.
            return false;
        }

        surface.ReapplyNonActivation();
        if (message == ActivationMessage.Activate)
        {
            _ = _keptActivations.Add(surface);
        }

        RequestRestore(surface, message, probableCause);
        return true;
    }

    /// <summary>
    /// Called by the common hook for <c>WM_ACTIVATE(WA_INACTIVE)</c>, <c>WM_NCACTIVATE(FALSE)</c> and
    /// <c>WM_ACTIVATEAPP(FALSE)</c>. Returns true for the <c>WA_INACTIVE</c> that ends an activation the hook kept from
    /// WPF, so the hook keeps that one too.
    /// </summary>
    internal bool OnDeactivated(NonActivatingWindow surface, ActivationMessage message) =>
        message == ActivationMessage.Activate && _keptActivations.Remove(surface);

    /// <summary>A destroyed surface will never be deactivated.</summary>
    internal void Forget(NonActivatingWindow surface) => _ = _keptActivations.Remove(surface);

    /// <summary>
    /// Counts the violation and queues one restoration request, unless one is already queued: then this activation
    /// joins it.
    /// </summary>
    private void RequestRestore(
        NonActivatingWindow surface,
        ActivationMessage message,
        ActivationCause probableCause
    )
    {
        if (_pending is not null)
        {
            return;
        }

        var violation = new ActivationViolation(
            surface.Id,
            surface.SurfaceWindow,
            message,
            probableCause,
            Clock.GetUtcNow()
        );
        _pending = violation;
        _ = surface.Dispatcher.InvokeAsync(SendPending, DispatcherPriority.Send);
        Interlocked.Increment(ref _violations);
        ViolationDetected?.Invoke(this, new ActivationViolationEventArgs(violation));
        Debug.Fail(
            string.Create(
                CultureInfo.InvariantCulture,
                $"REG-01 violation: surface {violation.Surface} was activated without a lease ({violation.Message}, probable cause {violation.ProbableCause})."
            )
        );
    }

    /// <summary>Hands the queued request to the arbiter, which restores from the thread pool and returns at once.</summary>
    private void SendPending()
    {
        var violation = _pending;
        _pending = null;
        if (violation is not null)
        {
            Arbiter.ReportViolation(violation);
        }
    }
}
