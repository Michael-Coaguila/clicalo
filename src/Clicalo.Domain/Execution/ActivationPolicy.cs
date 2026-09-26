using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The single activation flow of every surface and origin (EJE-001, blueprint §7.2), in normative order: touch
/// filter → edit mode → test mode → elevation → incomplete or blocked → confirmation → execute. Dimming never changes
/// the result (EJE-017).
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class ActivationPolicy
{
    /// <summary>Decides what an activation does.</summary>
    /// <param name="context">Everything the decision needs.</param>
    public static ActivationOutcome Decide(in ActivationContext context) =>
        throw new NotImplementedException();
}
