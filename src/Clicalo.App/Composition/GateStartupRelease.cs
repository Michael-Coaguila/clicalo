using Clicalo.App.Lifecycle;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.App.Composition;

/// <summary>The preventive release of a sending start, through the gate with the menu mask (SEG-006).</summary>
/// <param name="gate">The gate of the engine's ledger.</param>
internal sealed class GateStartupRelease(InjectionGate gate) : IStartupRelease
{
    /// <inheritdoc />
    public int ReleaseStuckModifiers() => PreventiveRelease.Run(gate, gate.Ledger.Generation);
}
