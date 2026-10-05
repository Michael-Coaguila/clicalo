using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// Recent Shift presses, so the fifth in a second is delayed and Windows Sticky Keys never triggers (SEG-008,
/// <c>Timings.KeySafety.ShiftBurstLimit</c>).
/// </summary>
/// <param name="RecentShiftTicks">Ticks of the Shift presses inside the current window, oldest first.</param>
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
    public long NextAllowed(long nowTicks, int maxCount, long windowTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfNegative(windowTicks);

        // Presses that still count at nowTicks: those after nowTicks - windowTicks.
        var inWindow = RecentShiftTicks.Items.Where(t => t > nowTicks - windowTicks).ToArray();
        if (inWindow.Length < maxCount)
        {
            return nowTicks;
        }

        // One more press is allowed once the oldest press that would make maxCount + 1 leaves the window.
        var blocking = inWindow[inWindow.Length - maxCount];
        return Math.Max(nowTicks, blocking + windowTicks);
    }

    /// <summary>Records a Shift press and drops the ones that left the window.</summary>
    /// <param name="atTicks">When it was sent.</param>
    /// <param name="windowTicks">Window length in ticks.</param>
    public ShiftBurstWindow Record(long atTicks, long windowTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowTicks);
        var kept = RecentShiftTicks
            .Items.Where(t => t > atTicks - windowTicks)
            .Append(atTicks)
            .Order()
            .ToImmutableArray();
        return new ShiftBurstWindow(new ValueList<long>(kept));
    }
}
