using System.Diagnostics;
using System.Globalization;
using Clicalo.Application.Ports;
using Windows.Win32;

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
/// <c>WM_NCACTIVATE</c> and <c>WM_ACTIVATE</c> to the activated one): they count as ONE violation, which stays open
/// until the surface is deactivated (<c>WM_ACTIVATE(WA_INACTIVE)</c>) or the application loses the activation
/// (<c>WM_ACTIVATEAPP(FALSE)</c>). <c>WM_ACTIVATEAPP(TRUE)</c> is sent to every top-level window of the thread, also
/// when a window that is not a surface (the Control Center under its lease) is activated, so it only counts on the
/// surface that owns the foreground.
/// </para>
/// </remarks>
public sealed class ActivationGuard
{
    /// <summary>Name of the violation counter in metrics and logs.</summary>
    public const string MetricName = "reg01.violations";

    private long _violations;
    private NonActivatingWindow? _openViolation;

    /// <summary>Creates the guard that reports to <paramref name="arbiter"/> and stamps with <paramref name="timeProvider"/>.</summary>
    public ActivationGuard(IActivationArbiter arbiter, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Arbiter = arbiter;
        Clock = timeProvider;
    }

    /// <summary>Violations detected since the process started (<see cref="MetricName"/>).</summary>
    public long Violations => Interlocked.Read(ref _violations);

    /// <summary>Raised on the UI thread after each violation has been reported and repaired.</summary>
    public event EventHandler<ActivationViolationEventArgs>? ViolationDetected;

    /// <summary>The orchestrator side that decides whether an activation is leased.</summary>
    public IActivationArbiter Arbiter { get; }

    /// <summary>Clock for <see cref="ActivationViolation.DetectedAt"/>.</summary>
    public TimeProvider Clock { get; }

    /// <summary>
    /// Called by the common hook of <paramref name="surface"/> for each activation message. Returns true when the
    /// activation is a violation: the first message of it has been reported, repaired and counted, and the later
    /// messages of the same activation return true without counting again.
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
        if (window.IsNone || Arbiter.IsActivationLeased(window))
        {
            return false;
        }

        if (message == ActivationMessage.ActivateApp && !OwnsForeground(window))
        {
            // Another window of this thread is being activated: its own messages decide.
            return false;
        }

        if (_openViolation is not null)
        {
            return true;
        }

        _openViolation = surface;
        var violation = new ActivationViolation(
            surface.Id,
            window,
            message,
            probableCause,
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
        return true;
    }

    /// <summary>
    /// Called by the common hook for <c>WM_ACTIVATE(WA_INACTIVE)</c> and <c>WM_ACTIVATEAPP(FALSE)</c>: closes the open
    /// violation when its surface, or the whole application, loses the activation. Returns true for the
    /// <c>WA_INACTIVE</c> that ends an activation the hook kept from WPF, so the hook keeps that one too.
    /// </summary>
    internal bool OnDeactivated(NonActivatingWindow surface, ActivationMessage message)
    {
        var open = _openViolation;
        if (open is null)
        {
            return false;
        }

        var sameSurface = ReferenceEquals(open, surface);
        if (message == ActivationMessage.ActivateApp || sameSurface)
        {
            _openViolation = null;
        }

        return sameSurface && message == ActivationMessage.Activate;
    }

    /// <summary>A destroyed surface can no longer be deactivated: its open violation ends with it.</summary>
    internal void Forget(NonActivatingWindow surface)
    {
        if (ReferenceEquals(_openViolation, surface))
        {
            _openViolation = null;
        }
    }

    private static bool OwnsForeground(WindowToken window) =>
        (nint)PInvoke.GetForegroundWindow() == window.Handle;

    /// <summary>Counts <paramref name="violation"/> and raises <see cref="ViolationDetected"/>.</summary>
    private void Record(ActivationViolation violation)
    {
        Interlocked.Increment(ref _violations);
        ViolationDetected?.Invoke(this, new ActivationViolationEventArgs(violation));
    }
}
