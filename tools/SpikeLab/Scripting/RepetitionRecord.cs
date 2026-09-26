using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>One repetition of a step, with its outcome and why.</summary>
/// <param name="Index">1-based position within the step's current attempt.</param>
/// <param name="At">When it closed.</param>
/// <param name="Source">Who closed it.</param>
/// <param name="Passed">True when it passed every automatic check and the maintainer did not mark it as failed.</param>
/// <param name="Problems">Why it failed: the automatic problems and, if any, the maintainer's mark.</param>
/// <param name="Evidence">What was measured.</param>
internal sealed record RepetitionRecord(
    int Index,
    DateTimeOffset At,
    RepetitionSource Source,
    bool Passed,
    ImmutableArray<string> Problems,
    RepetitionEvidence Evidence
)
{
    /// <summary>True when the maintainer marked it with «Falló».</summary>
    public bool FailedByUser { get; init; }
}
