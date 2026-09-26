using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Touch;

/// <summary>
/// The filter values and gesture thresholds of one contact, scaled once to physical pixels when it goes down: a
/// contact finishes with the values it started with, even if <see cref="GestureRecognizer.Configure"/> changes them.
/// </summary>
/// <param name="Settings">The filter values in logical pixels, passed to <see cref="TouchFilter"/>.</param>
/// <param name="DpiScale">Physical pixels per logical pixel.</param>
/// <param name="HitSlop">Extra hit area around each target, in physical pixels.</param>
/// <param name="CancelMove">Cancel distance in physical pixels; zero when the check is off.</param>
/// <param name="MoveSlop">
/// Distance after which the contact counts as moving and can no longer become a long press: the drag threshold
/// max(<c>Timings.Touch.DragMinDistancePx</c>, cancel distance), in physical pixels.
/// </param>
/// <param name="SwipeMin">Horizontal travel that a swipe must exceed, in physical pixels.</param>
/// <param name="PalmMin">Contact width or height from which the contact is a palm, in physical pixels.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ContactThresholds(
    TouchSettings Settings,
    double DpiScale,
    int HitSlop,
    double CancelMove,
    double MoveSlop,
    double SwipeMin,
    double PalmMin
)
{
    /// <summary>Scales <paramref name="settings"/> and the <c>Timings.Touch</c> thresholds to physical pixels.</summary>
    public static ContactThresholds From(in TouchSettings settings, double dpiScale) =>
        new(
            settings,
            dpiScale,
            (int)Math.Round(settings.HitSlopPx * dpiScale, MidpointRounding.AwayFromZero),
            settings.CancelMovePx * dpiScale,
            Math.Max(Timings.Touch.DragMinDistancePx, settings.CancelMovePx) * dpiScale,
            Timings.Touch.SwipeMinDistancePx * dpiScale,
            Timings.Touch.PalmContactMinPx * dpiScale
        );

    /// <summary>True when <paramref name="contact"/> is palm-sized (ACC-007).</summary>
    public bool IsPalm(PhysicalRect contact) =>
        !contact.IsEmpty && Math.Max(contact.Width, contact.Height) >= PalmMin;

    /// <summary>True when the displacement exceeds a cancel distance that is switched on.</summary>
    public bool ExceedsCancelMove(double displacement) =>
        CancelMove > 0 && displacement > CancelMove;

    /// <summary>
    /// True when a contact that went down at <paramref name="down"/> and is now at <paramref name="current"/> is a
    /// horizontal page swipe (CUA-005): |dx| &gt; <see cref="SwipeMin"/> and
    /// |dy| &lt; <c>Timings.Touch.SwipeMaxSlope</c> × |dx|.
    /// </summary>
    public bool IsSwipe(PhysicalPoint down, PhysicalPoint current)
    {
        double dx = Math.Abs((long)current.X - down.X);
        double dy = Math.Abs((long)current.Y - down.Y);
        return dx > SwipeMin && dy < Timings.Touch.SwipeMaxSlope * dx;
    }
}
