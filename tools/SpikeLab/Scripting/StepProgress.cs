using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>Where one step of a script stands: its repetitions, its confirmation and its verdict. Immutable.</summary>
/// <param name="Step">The step.</param>
internal sealed record StepProgress(ScriptStep Step)
{
    /// <summary>The repetitions of the current attempt, in order.</summary>
    public ImmutableArray<RepetitionRecord> Repetitions { get; init; } = [];

    /// <summary>Attempts thrown away with «Repetir».</summary>
    public ImmutableArray<DiscardedAttempt> Discarded { get; init; } = [];

    /// <summary>The maintainer confirmed the final check with «Funcionó» (automatic steps).</summary>
    public bool Confirmed { get; init; }

    /// <summary>The step has been the current step at some point.</summary>
    public bool Visited { get; init; }

    /// <summary>The maintainer moved on with «Siguiente».</summary>
    public bool Left { get; init; }

    /// <summary>When the step first became current.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Repetitions that passed.</summary>
    public int PassedCount
    {
        get
        {
            var count = 0;
            foreach (var repetition in Repetitions)
            {
                if (repetition.Passed)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Repetitions that failed.</summary>
    public int FailedCount => Repetitions.Length - PassedCount;

    /// <summary>True when the required repetitions are there, whatever their outcome.</summary>
    public bool HasAllRepetitions => Repetitions.Length >= Step.Required;

    /// <summary>The verdict of the step (see <see cref="StepVerdict"/>).</summary>
    public StepVerdict Verdict
    {
        get
        {
            if (FailedCount > 0)
            {
                return StepVerdict.Failed;
            }

            if (PassedCount >= Step.Required && (Confirmed || !Step.NeedsConfirmation))
            {
                return StepVerdict.Passed;
            }

            if (Left)
            {
                return Repetitions.IsEmpty && Step.Optional
                    ? StepVerdict.NotApplicable
                    : StepVerdict.Incomplete;
            }

            return Visited ? StepVerdict.InProgress : StepVerdict.Pending;
        }
    }
}
