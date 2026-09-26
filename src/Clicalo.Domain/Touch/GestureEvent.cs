using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// One output of <see cref="GestureRecognizer"/>: a gesture on a target, a swipe or an ignored contact
/// (blueprint §7.8). Only the members that apply to <see cref="Kind"/> carry a value; the others keep their defaults.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="PointerId">The contact that produced it.</param>
/// <param name="Target">The resolved target; null for a swipe and for a contact without target.</param>
/// <param name="Position">Where the contact was when the event was produced, in physical screen pixels.</param>
/// <param name="Timestamp">When it was produced, on the recognizer's timeline.</param>
/// <param name="Ignored">Why the contact was ignored; <see cref="IgnoreReason.None"/> otherwise.</param>
/// <param name="HoldEnd">Why the hold ended; <see cref="HoldEndReason.None"/> otherwise.</param>
/// <param name="Swipe">Direction of the swipe; <see cref="SwipeDirection.None"/> otherwise.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct GestureEvent(
    GestureKind Kind,
    uint PointerId,
    TouchTargetId? Target,
    PhysicalPoint Position,
    DateTimeOffset Timestamp,
    IgnoreReason Ignored = IgnoreReason.None,
    HoldEndReason HoldEnd = HoldEndReason.None,
    SwipeDirection Swipe = SwipeDirection.None
);
