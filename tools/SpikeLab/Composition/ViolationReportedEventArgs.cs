using Clicalo.Application.Ports;

namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>Data of <see cref="ArbiterRelay.ViolationReported"/>.</summary>
/// <param name="violation">The violation.</param>
/// <param name="forwarded">True when the orchestrator received it (and restores the foreground).</param>
internal sealed class ViolationReportedEventArgs(ActivationViolation violation, bool forwarded)
    : EventArgs
{
    /// <summary>The violation.</summary>
    public ActivationViolation Violation { get; } = violation;

    /// <summary>True when the orchestrator received it.</summary>
    public bool Forwarded { get; } = forwarded;
}
