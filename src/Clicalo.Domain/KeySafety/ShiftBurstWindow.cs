using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// Recent Shift presses, so the fifth in a second is delayed and Windows Sticky Keys never triggers (SEG-008,
/// <c>Timings.KeySafety.ShiftBurstLimit</c>).
/// </summary>
/// <param name="RecentShiftTicks">Ticks of the Shift presses inside the current window, oldest first.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record ShiftBurstWindow(ValueList<long> RecentShiftTicks)
{
    /// <summary>No recent Shift press.</summary>
    public static ShiftBurstWindow Empty { get; } = new([]);

    /// <summary>
    /// The earliest tick at which one more Shift press keeps at most <paramref name="maxCount"/> presses inside any
    /// window of <paramref name="windowTicks"/>; <paramref name="nowTicks"/> when it can go now.
    /// </summary>
    /// <param name="nowTicks">Now.</param>
    /// <param name="maxCount">Presses allowed per window.</param>
    /// <param name="windowTicks">Window length in ticks.</param>
    public long NextAllowed(long nowTicks, int maxCount, long windowTicks) =>
        throw new NotImplementedException();

    /// <summary>Records a Shift press and drops the ones that left the window.</summary>
    /// <param name="atTicks">When it was sent.</param>
    /// <param name="windowTicks">Window length in ticks.</param>
    public ShiftBurstWindow Record(long atTicks, long windowTicks) =>
        throw new NotImplementedException();
}
