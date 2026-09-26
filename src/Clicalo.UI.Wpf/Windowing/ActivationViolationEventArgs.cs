using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>Data of <see cref="ActivationGuard.ViolationDetected"/>.</summary>
/// <param name="violation">The detected violation.</param>
public sealed class ActivationViolationEventArgs(ActivationViolation violation) : EventArgs
{
    /// <summary>The detected violation.</summary>
    public ActivationViolation Violation { get; } =
        violation ?? throw new ArgumentNullException(nameof(violation));
}
