using Clicalo.Application.Ports;

namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>
/// Breaks the construction cycle of the foreground pieces: <c>ActivationGuard</c> needs its
/// <see cref="IActivationArbiter"/> at construction, the arbiter is the <c>ForegroundOrchestrator</c>, and the
/// orchestrator needs <c>SurfaceRegistry</c>, which needs the guard. The relay forwards to the orchestrator once it
/// exists; before that (or when one of its ports failed to start) no activation is leased and violations are only
/// counted and reported to the laboratory.
/// </summary>
internal sealed class ArbiterRelay : IActivationArbiter
{
    private IActivationArbiter? _target;

    /// <summary>Raised on the UI thread for every violation reported, whether or not the target exists.</summary>
    public event EventHandler<ViolationReportedEventArgs>? ViolationReported;

    /// <summary>The orchestrator, once created.</summary>
    public IActivationArbiter? Target
    {
        get => Volatile.Read(ref _target);
        set => Volatile.Write(ref _target, value);
    }

    /// <inheritdoc />
    public bool IsActivationLeased(WindowToken window) =>
        Target?.IsActivationLeased(window) ?? false;

    /// <inheritdoc />
    public void ReportViolation(ActivationViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        ViolationReported?.Invoke(
            this,
            new ViolationReportedEventArgs(violation, Target is not null)
        );
        Target?.ReportViolation(violation);
    }
}
