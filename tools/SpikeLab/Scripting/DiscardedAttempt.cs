using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// An attempt of a step that the maintainer threw away with «Repetir». It stays in the report, so a repeated cycle
/// never hides what happened.
/// </summary>
/// <param name="At">When it was discarded.</param>
/// <param name="Repetitions">Its repetitions.</param>
/// <param name="Confirmed">Whether its final check had been confirmed.</param>
internal sealed record DiscardedAttempt(
    DateTimeOffset At,
    ImmutableArray<RepetitionRecord> Repetitions,
    bool Confirmed
);
