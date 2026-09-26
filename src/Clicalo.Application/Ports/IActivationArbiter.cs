namespace Clicalo.Application.Ports;

/// <summary>
/// The side of <c>ForegroundOrchestrator</c> that <c>ActivationGuard</c> (UI.Wpf) talks to, since UI.Wpf only sees
/// Application through Ports (blueprint §3.5, §4.2). Implemented by
/// <c>Clicalo.Application.Foreground.ForegroundOrchestrator</c>.
/// </summary>
public interface IActivationArbiter
{
    /// <summary>
    /// True when a lease is active whose target is <paramref name="window"/>: the activation is legitimate. Called
    /// synchronously from the surface's window procedure, so it reads a published snapshot and never blocks.
    /// </summary>
    bool IsActivationLeased(WindowToken window);

    /// <summary>
    /// Reports a violation. Returns at once; the orchestrator then restores the last verified external foreground
    /// (<c>RestoreAfterViolationAsync</c>) within <c>Timings.Windowing.ViolationRestoreBudget</c>.
    /// </summary>
    void ReportViolation(ActivationViolation violation);
}
