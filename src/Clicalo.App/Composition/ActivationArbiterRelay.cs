using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// Breaks the construction cycle of the windowing pieces (blueprint §3.5, §3.6): <c>ActivationGuard</c> needs its
/// <see cref="IActivationArbiter"/> at construction, the arbiter is the <c>ForegroundOrchestrator</c>, and the
/// orchestrator needs the <c>SurfaceRegistry</c>, which needs the guard. The relay forwards to the orchestrator once
/// it exists; before that no activation is leased and a violation is only counted by the guard.
/// </summary>
internal sealed class ActivationArbiterRelay : IActivationArbiter
{
    private IActivationArbiter? _target;

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
    public void ReportViolation(ActivationViolation violation) =>
        Target?.ReportViolation(violation);
}
