using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Library;

/// <summary>
/// Wait before the next step: a timer of the engine, never <c>Thread.Sleep</c> (blueprint §3.2, rule 4). The duration
/// is always inside <c>Timings.Macro.MacroWaitRange</c> (invariant I6), checked here.
/// </summary>
public sealed record WaitStep : MacroStep
{
    /// <summary>Creates a wait.</summary>
    /// <param name="duration">How long; inside <c>Timings.Macro.MacroWaitRange</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The duration is outside the range: a defect of the caller, which clamps user input before.
    /// </exception>
    public WaitStep(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, Timings.Macro.MacroWaitRange.Min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(duration, Timings.Macro.MacroWaitRange.Max);
        Duration = duration;
    }

    /// <summary>How long the macro waits.</summary>
    public TimeSpan Duration { get; }
}
