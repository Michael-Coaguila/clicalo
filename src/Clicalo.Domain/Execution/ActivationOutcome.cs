using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>The decision and the tile's next filter state (an ignored touch never restarts the window, TAC-002).</summary>
/// <param name="Decision">What to do.</param>
/// <param name="NextFilter">The tile's filter state afterwards.</param>
public readonly record struct ActivationOutcome(
    ActivationDecision Decision,
    ButtonFilterState NextFilter
);
