using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// A touchable control of a surface as the recognizer sees it: its bounds in physical screen pixels and the gestures
/// it accepts. The extra hit area (<see cref="TouchSettings.HitSlopPx"/>) is added by the recognizer, which resolves
/// overlaps by the nearest center (TAC-002, REG-02).
/// </summary>
/// <param name="Id">Surface-local identifier.</param>
/// <param name="Bounds">Visible bounds, in physical screen pixels.</param>
/// <param name="Kind">Accepted gestures.</param>
/// <param name="InScrollZone">
/// Whether the target lies in a zone that a finger scrolls (TAC-004). A <see cref="TouchTargetKind.Hold"/> there does
/// not start holding until the contact has rested <c>Timings.Touch.HoldInScrollDelay</c> without moving past the drag
/// threshold: until then the gesture may still be a scroll, and a scroll must not press keys.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TouchTarget(
    TouchTargetId Id,
    PhysicalRect Bounds,
    TouchTargetKind Kind,
    bool InScrollZone = false
);
