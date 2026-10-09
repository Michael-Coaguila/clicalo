using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Execution;

/// <summary>What happens when the queued steps of an execution are done (EJE-012).</summary>
/// <param name="Origin">The execution.</param>
/// <param name="Notice">A notice to show, if any.</param>
/// <param name="CountsUsage">Whether it counts as a use (Frequents, FRE-002).</param>
/// <param name="ContinueMacro">The macro run to continue, when the steps are a keys step of a macro.</param>
public sealed record StepCompletion(
    ExecutionOrigin Origin,
    Message? Notice,
    bool CountsUsage,
    MacroRunId? ContinueMacro
)
{
    /// <summary>
    /// Whether a counted use also becomes the last action for Repeat (AVI-004): every action but Hold and Toggle.
    /// </summary>
    public bool Repeatable { get; init; } = true;
}
