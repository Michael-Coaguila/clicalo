using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;

namespace Clicalo.Platform.Windows.PointerTracking;

/// <summary>
/// Where the cursor was outside Clícalo's windows (EJE-009, D21): the last point seen there, and the last one where it
/// rested for <c>Timings.Mouse.PointerRest</c> before it moved on. A finger jumps onto the panel, but a touchpad or a
/// mouse slides into it, so the last point is the border it crossed; <see cref="Place"/> is the one it rested on, and the
/// last point only while there is none. Immutable: the SysEvents thread replaces it, the engine reads it.
/// </summary>
/// <param name="Last">The last point seen outside Clícalo, or <see langword="null"/>.</param>
/// <param name="Rested">The last point where the cursor rested outside Clícalo, or <see langword="null"/>.</param>
/// <param name="LastAt">When <paramref name="Last"/> was seen (a <see cref="TimeProvider"/> timestamp).</param>
/// <param name="Pending">Whether the first move after <paramref name="Last"/> is still to come.</param>
internal readonly record struct CursorTrail(
    PhysicalPoint? Last,
    PhysicalPoint? Rested,
    long LastAt,
    bool Pending
)
{
    /// <summary>The place a mouse action goes to: where the cursor rested, else the last point outside.</summary>
    public PhysicalPoint? Place => Rested ?? Last;

    /// <summary>The cursor was seen at <paramref name="point"/>, outside Clícalo, at <paramref name="now"/>.</summary>
    /// <param name="point">Where.</param>
    /// <param name="now">When (a timestamp of <paramref name="time"/>).</param>
    /// <param name="time">The clock of the timestamps.</param>
    public CursorTrail Outside(PhysicalPoint point, long now, TimeProvider time) =>
        Settle(now, time) with
        {
            Last = point,
            LastAt = now,
            Pending = true,
        };

    /// <summary>The cursor was seen on a window of Clícalo at <paramref name="now"/>.</summary>
    /// <param name="now">When (a timestamp of <paramref name="time"/>).</param>
    /// <param name="time">The clock of the timestamps.</param>
    public CursorTrail OnClicalo(long now, TimeProvider time) => Settle(now, time);

    /// <summary>
    /// At the first move after <see cref="Last"/>: when the cursor stayed there long enough, that is where the person
    /// works. Later samples (on Clícalo, or the round trip of a wheel step) say nothing about that point.
    /// </summary>
    private CursorTrail Settle(long now, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        if (!Pending)
        {
            return this;
        }

        var rested =
            Last is { } last && time.GetElapsedTime(LastAt, now) >= Timings.Mouse.PointerRest
                ? last
                : Rested;
        return this with { Rested = rested, Pending = false };
    }
}
