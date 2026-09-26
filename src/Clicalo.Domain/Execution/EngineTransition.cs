using System.Collections.Immutable;

namespace Clicalo.Domain.Execution;

/// <summary>The result of one step of the reducer.</summary>
/// <param name="Next">The new state.</param>
/// <param name="Effects">What the host must do, in order.</param>
public sealed record EngineTransition(EngineState Next, ImmutableArray<EngineEffect> Effects);
