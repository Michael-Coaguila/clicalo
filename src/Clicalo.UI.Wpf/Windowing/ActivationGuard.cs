using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// REG-01 at run time (blueprint §3.5, ADR-0005). Fed synchronously by the activation messages of the surfaces
/// themselves (<c>WM_ACTIVATE</c> other than <c>WA_INACTIVE</c>, <c>WM_NCACTIVATE(TRUE)</c>,
/// <c>WM_ACTIVATEAPP(TRUE)</c>) on the UI thread, so it does not depend on WinEvents (which skip the own process)
/// and also catches Clícalo itself being in front (EC-EJE-05).
/// </summary>
/// <remarks>
/// On an activation: if <see cref="IActivationArbiter.IsActivationLeased"/> says a lease targets the window, it is
/// legitimate. Otherwise it is a VIOLATION: <see cref="IActivationArbiter.ReportViolation"/> (the orchestrator
/// restores the last verified external foreground within <c>Timings.Windowing.ViolationRestoreBudget</c>),
/// <c>WS_EX_NOACTIVATE</c> is applied again, <see cref="Violations"/> (metric <see cref="MetricName"/>) increases by
/// one, <see cref="ViolationDetected"/> is raised and, in Debug and CI builds, <c>Debug.Fail</c> stops the run.
/// </remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class ActivationGuard
{
    /// <summary>Name of the violation counter in metrics and logs.</summary>
    public const string MetricName = "reg01.violations";

    private long _violations;

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
    /// activation was a violation (and has been reported, repaired and counted).
    /// </summary>
    /// <param name="surface">The surface that received the message.</param>
    /// <param name="message">Which activation message.</param>
    /// <param name="probableCause">What the hook saw just before (DPI change, topmost, show).</param>
    public bool OnActivated(
        NonActivatingWindow surface,
        ActivationMessage message,
        ActivationCause probableCause
    ) => throw new NotImplementedException("M1 windowing package.");

    /// <summary>Counts <paramref name="violation"/> and raises <see cref="ViolationDetected"/>.</summary>
    private void Record(ActivationViolation violation)
    {
        Interlocked.Increment(ref _violations);
        ViolationDetected?.Invoke(this, new ActivationViolationEventArgs(violation));
    }
}
