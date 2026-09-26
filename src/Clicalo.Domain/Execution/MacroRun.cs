using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>A running macro as a resumable state machine (blueprint §7.3): step <c>i/n</c>, waiting until a tick.</summary>
/// <param name="Id">This run.</param>
/// <param name="Shortcut">The macro.</param>
/// <param name="StepIndex">The current step, zero-based.</param>
/// <param name="StepCount">Number of steps.</param>
/// <param name="WaitingUntilTicks">End of the current wait, or <see langword="null"/>.</param>
public sealed record MacroRun(
    MacroRunId Id,
    ShortcutId Shortcut,
    int StepIndex,
    int StepCount,
    long? WaitingUntilTicks
)
{
    /// <summary>The steps being run.</summary>
    public ValueList<MacroStep> Steps { get; init; }

    /// <summary>Where and how it was started: every step sends with this epoch, target and mode.</summary>
    public ExecutionOrigin? Origin { get; init; }
}
