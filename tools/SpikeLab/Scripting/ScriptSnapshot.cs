using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>An immutable picture of a script run: what the guide strip shows and what the report stores.</summary>
/// <param name="Script">The script.</param>
/// <param name="Steps">The progress of every step, in script order.</param>
/// <param name="CurrentIndex">The current step; equal to the step count once the script is finished.</param>
/// <param name="StartedAt">When the run started.</param>
internal sealed record ScriptSnapshot(
    SpikeScript Script,
    ImmutableArray<StepProgress> Steps,
    int CurrentIndex,
    DateTimeOffset StartedAt
)
{
    /// <summary>True once the maintainer moved past the last step.</summary>
    public bool IsFinished => CurrentIndex >= Steps.Length;

    /// <summary>The current step, or null when finished.</summary>
    public StepProgress? Current => IsFinished ? null : Steps[CurrentIndex];

    /// <summary>The verdict of the run (<see cref="Decide"/>).</summary>
    public SpikeVerdict Verdict => Decide(Steps);

    /// <summary>
    /// The decision rule shared by S1, S3 and S4: a failed decisive step fails the spike; the spike passes when every
    /// step passed, does not apply or is a non-decisive failure (reported apart); otherwise it is incomplete.
    /// </summary>
    public static SpikeVerdict Decide(ImmutableArray<StepProgress> steps)
    {
        var complete = true;
        foreach (var progress in steps)
        {
            switch (progress.Verdict)
            {
                case StepVerdict.Failed when progress.Step.Decisive:
                    return SpikeVerdict.Failed;
                case StepVerdict.Passed:
                case StepVerdict.NotApplicable:
                case StepVerdict.Failed:
                    break;
                default:
                    complete = false;
                    break;
            }
        }

        return complete && !steps.IsEmpty ? SpikeVerdict.Passed : SpikeVerdict.Incomplete;
    }
}
