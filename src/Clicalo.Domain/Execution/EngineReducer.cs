using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The functional core of the engine (blueprint §7.3, ADR-0004): a pure function from state and event to a new state
/// and effects, with <see cref="ActivationPolicy"/> and one planner per <see cref="Library.ActionKind"/> inside.
/// INV-1 to INV-12 (§7.5) are properties of this function, checked on every step with CsCheck.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class EngineReducer
{
    /// <summary>One step of the engine.</summary>
    /// <param name="state">The current state.</param>
    /// <param name="engineEvent">The event.</param>
    /// <param name="config">The settings the engine obeys.</param>
    /// <param name="nowTicks">Now, in <see cref="TimeProvider"/> ticks.</param>
    public static EngineTransition Reduce(
        EngineState state,
        EngineEvent engineEvent,
        EngineConfig config,
        long nowTicks
    ) => throw new NotImplementedException();
}
