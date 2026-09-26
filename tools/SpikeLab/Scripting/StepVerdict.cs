namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>The state of one step of a script.</summary>
internal enum StepVerdict
{
    /// <summary>Not reached yet.</summary>
    Pending,

    /// <summary>The current step, not finished.</summary>
    InProgress,

    /// <summary>All the required repetitions passed (and the final check was confirmed, for automatic steps).</summary>
    Passed,

    /// <summary>At least one repetition failed.</summary>
    Failed,

    /// <summary>Left before reaching the required repetitions.</summary>
    Incomplete,

    /// <summary>An optional step left without any repetition (missing pen, mouse, second monitor or virtual machine).</summary>
    NotApplicable,
}
