using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>An activation of a tile, before the policy decides (blueprint §7.1).</summary>
/// <param name="Phase">Which part arrives.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="ContactId">Pointer id of the contact; <see langword="null"/> for an invocation.</param>
/// <param name="Contact">Summary of an ended contact (duration, displacement, palm); <see langword="null"/> otherwise.</param>
/// <param name="At">When it happened.</param>
public readonly record struct ActivationRequest(
    ActivationPhase Phase,
    ActivationOrigin Origin,
    int? ContactId,
    ContactSummary? Contact,
    DateTimeOffset At
);
